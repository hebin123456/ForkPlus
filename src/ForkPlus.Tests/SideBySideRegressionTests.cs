// 回归测试（2026-09-15，"FileDiff SideBySide 三联症"修复产物）：
//   症状1：Add 场景拖左侧水平滚动条，右侧视图动但右侧滚动条 thumb 不动（两滚动条失步）。
//     根因：TouchpadAwareScrollViewer.ScrollBy / ScrollViewerWheelFix.ScrollBy 用
//     `ScrollBar.Value = x` 本地值赋值，永久压死 ScrollBar.AttachToScrollViewer 的
//     Template 优先级 Value↔Offset 绑定（IfUnset 语义破坏）→ 滚过一次滚轮后任何
//     程序化 Offset 写入（SideBySide 左右同步）只动视图不动 thumb。诊断探针实证：
//     Offset=300 时 hbar.Value 停在 50。修复：改用 SetCurrentValue（不改变值来源
//     优先级，绑定继续双向生效，Avalonia 原生拖拽路径同款 API）。
//   症状2/3：行号与代码垂直错位；左右视图行不对齐（common.ts 1226 行中文注释实测）。
//     根因：Avalonia TextLine 行高 = 行内所有 run 的 max 字体度量，全局 CJK 回退
//     Noto Sans CJK SC 行框更高（v4.1.2 修复时探针实测 CJK 自然高 21.05px@Win/
//     18.82px@Linux、ASCII 15.22/15.13px），行槽 = max(自然高, DefaultTextHeight
//     ×1.16) → CJK 行槽超默认槽，左右内容不同时逐行累积错位。
//     修复（v4.1.2）：SideBySideLineHeightSynchronizer 把两侧 LineHeightFactor 同步
//     抬到最大自然行高/DefaultTextHeight（行槽统一，左右逐行等高）；行号/±标记改
//     基线对齐（ClearTypeLineNumberMargin.GetLineTextBaselineY）。
//     修复（v4.1.4，行间距收紧，两步）：
//     第一步：内嵌 Noto Sans CJK SC 子集垂直度量收紧到 1.25em（CJK 自然高 16.25px
//     ≤ 默认槽 17.66px），同步器不再抬 factor——行间距回到"默认"（factor 1.16 的槽）。
//     第二步（"同区域 WPF 3.13.2 显示 43 行、Avalonia 只有 36 行"）：WPF AvalonEdit
//     行槽 = 自然行高；AvaloniaEdit 默认 LineHeightFactor=1.16 把槽再放大 16%（15.22
//     → 17.66px）→ 可见行数少 16%（43→36）。CodeEditor/HexEditor 显式置 1.0 后槽 =
//     ASCII 自然高（15.22px@Win/15.13px@Linux），CJK 度量随之再收紧到 1.16em（15.08px
//     ≤ 新槽，hhea/OS/2 win/typo 三处一致；否则同步器会把全部行槽抬到 16.25px，比 WPF
//     宽 7%）。行号按基线绘制，不受字体度量影响。
// 本测试守卫四条防线：滚轮后 thumb 仍跟随程序化滚动；CJK 混排左右行像素对齐；
// CJK 字体度量不超默认行槽（factor=1.0 口径，行间距不回弹）；纯 ASCII 文件 factor
// 保持初始值 1.0（行密度与 WPF 一致，DefaultLineHeight=自然行高）。
using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Rendering;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Diff;
using Xunit;
using Range = ForkPlus.Range;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class SideBySideRegressionTests
	{
		private const string ReportPath = @"C:\Users\H00518~1\AppData\Local\Temp\opencode\sxs_regression.txt";

		private static ScrollViewer GetSv(DiffCodeEditor editor)
		{
			return editor.GetVisualDescendants().OfType<ScrollViewer>().First((ScrollViewer x) => x.Name == "PART_ScrollViewer");
		}

		private static ScrollBar GetNamedBar(ScrollViewer sv, string name)
		{
			return sv.GetVisualDescendants().OfType<ScrollBar>().First((ScrollBar b) => b.Name == name);
		}

		// Add 场景：新增文件，右侧 rows 行（每 5 行 1 行含中文注释，其余 ASCII），左侧全为对齐空行。
		// wide=true 时行宽 ~220 字符保证两侧水平可滚（复现症状 1 的水平滚动条路径）。
		private static Diff MakeAddDiff(int rows, bool wide, bool withCjk)
		{
			var lines = new System.Collections.Generic.List<string>();
			for (int i = 0; i < rows; i++)
			{
				bool cjk = withCjk && i % 5 == 0;
				string tail = wide ? new string('x', 180) : "";
				lines.Add(cjk
					? "// 中文注释行第" + i + "行验证回退字体行高" + tail + "\n"
					: "export function helper" + i + "() { return " + i + "; } " + tail + "\n");
			}
			var subChunk = new SubChunk(
				new Range(0, 0),
				new Range(0, 0),
				new Range(0, lines.Count),
				new Range(lines.Count, lines.Count),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(0, 0, 1, lines.Count, null, new[] { subChunk });
			return new Diff("new.ts", "new.ts", null, null, "111", "222", lines.ToArray(), new[] { chunk }, null, Diff.FileType.Text, false);
		}

		[Fact]
		public void SideBySide_WheelScroll_ThenSyncDrag_ThumbStillFollows()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report;
			var sbHolder = new StringBuilder[1];
			try
			{
				report = Dispatcher.UIThread.InvokeAsync(delegate
				{
					var sb = new StringBuilder();
					sbHolder[0] = sb;
					var control = new SideBySideTextDiffControl();
					var window = new Window { Width = 900, Height = 400, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.SetDiff(MakeAddDiff(30, wide: true, withCjk: true), 4, entireFile: true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();

					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					TextView rtv = right.TextArea.TextView;
					ScrollViewer leftSv = GetSv(left);
					ScrollViewer rightSv = GetSv(right);
					ScrollBar rightHBar = GetNamedBar(rightSv, "PART_HorizontalScrollBar");

					// 1) 前置：绑定存活时程序化写 Offset → thumb 跟随
					rightSv.Offset = rightSv.Offset.WithX(50.0);
					Dispatcher.UIThread.RunJobs();
					Assert.True(Math.Abs(rightHBar.Value - 50.0) < 1.5, "前置失败：绑定未生效");

					// 2) 模拟用户在右栏滚一次滚轮（TouchpadAwareScrollViewer.ScrollBy 私有路径）。
					//    修复前：`_vbar.Value = y` 本地值压死 Template 绑定，此后 thumb 永久冻结。
					var scrollBy = typeof(TouchpadAwareScrollViewer).GetMethod("ScrollBy",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					Assert.True(scrollBy != null, "TouchpadAwareScrollViewer.ScrollBy 反射失败（实现变更？）");
					scrollBy.Invoke(rightSv, new object[] { 0.0, 48.0 });
					Dispatcher.UIThread.RunJobs();
					sb.AppendLine("after wheel: offset=" + rightSv.Offset.Y.ToString("F1") + " hbar=" + rightHBar.Value.ToString("F1"));

					// 3) 模拟用户拖左侧水平滚动条 → SideBySide 同步链程序化写右侧 Offset。
					//    修复前：视图滚到 300 而 rightHBar.Value 冻结在 50（两滚动条失步）。
					leftSv.Offset = leftSv.Offset.WithX(300.0);
					Dispatcher.UIThread.RunJobs();
					sb.AppendLine("after drag: rightView.X=" + rtv.ScrollOffset.X.ToString("F1")
						+ " rightHBar.Value=" + rightHBar.Value.ToString("F1"));
					Assert.True(Math.Abs(rtv.ScrollOffset.X - 300.0) < 1.5,
						"右侧视图应跟随左侧滚动（实际 " + rtv.ScrollOffset.X.ToString("F1") + "）");
					Assert.True(Math.Abs(rightHBar.Value - 300.0) < 1.5,
						"滚轮滚动后右侧 thumb 应仍跟随程序化滚动（SetCurrentValue 保绑定）：hbar="
						+ rightHBar.Value.ToString("F1") + "，期望 ~300");

					window.Close();
					return sb.ToString();
				}).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				report = (sbHolder[0]?.ToString() ?? "") + "\nOUTER EXCEPTION: " + ex;
			}
			System.IO.File.WriteAllText(ReportPath, report);
			Assert.DoesNotContain("OUTER EXCEPTION", report);
		}

		[Fact]
		public void SideBySide_CjkMixed_AddScenario_RowsStayAligned()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report;
			var sbHolder = new StringBuilder[1];
			try
			{
				report = Dispatcher.UIThread.InvokeAsync(delegate
				{
					var sb = new StringBuilder();
					sbHolder[0] = sb;
					var control = new SideBySideTextDiffControl();
					var window = new Window { Width = 900, Height = 400, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.SetDiff(MakeAddDiff(30, wide: false, withCjk: true), 4, entireFile: true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();

					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					TextView ltv = left.TextArea.TextView;
					TextView rtv = right.TextArea.TextView;

					// 1) 行高统一（v4.1.4 两步收紧后）：CJK 自然行高 15.08px@13px ≤ 默认行槽
				//    （CodeEditor 置 factor=1.0 → 槽 = ASCII 自然高 ≈ 15.13px@Linux/
				//    15.22px@Win），行槽统一为自然行高密度——两侧 factor 相等且保持
				//    初始值 1.0（不再抬高），DefaultLineHeight ≥ 可见最大自然行高 →
				//    所有行槽等高。
				Assert.True(Math.Abs(left.Options.LineHeightFactor - right.Options.LineHeightFactor) < 0.001,
					"两侧 LineHeightFactor 应相等：left=" + left.Options.LineHeightFactor.ToString("F3")
					+ " right=" + right.Options.LineHeightFactor.ToString("F3"));
				Assert.True(left.Options.LineHeightFactor < 1.01,
					"v4.1.4 字体度量收紧（1.16em）后 CJK 自然行高应落入 factor=1.0 的默认行槽，"
					+ "LineHeightFactor 不应被抬高（实际 " + left.Options.LineHeightFactor.ToString("F3")
					+ "，若 >1.01 说明字体垂直度量回弹或 CodeEditor factor=1.0 丢失）");
					Assert.True(Math.Abs(ltv.DefaultLineHeight - rtv.DefaultLineHeight) < 0.01,
						"两侧 DefaultLineHeight 应相等");
					foreach (AvaloniaEdit.Rendering.VisualLine v in rtv.VisualLines)
					{
						Assert.True(Math.Abs(v.Height - rtv.DefaultLineHeight) < 0.01,
							"右侧行槽应统一为 DefaultLineHeight（line#" + v.FirstDocumentLine.LineNumber
							+ " 槽高 " + v.Height.ToString("F2") + " vs " + rtv.DefaultLineHeight.ToString("F2") + "）");
					}

					// 2) 左右行像素对齐：对应行 VisualTop 差 ≤ 0.5px（修复前 12 行漂移 13.55px）。
					var lv = ltv.VisualLines.ToArray();
					var rv = rtv.VisualLines.ToArray();
					Assert.True(lv.Length > 5 && rv.Length > 5, "应有足够可见行");
					int compare = Math.Min(lv.Length, rv.Length);
					for (int i = 0; i < compare; i++)
					{
						double delta = Math.Abs(rv[i].VisualTop - lv[i].VisualTop);
						Assert.True(delta < 0.5,
							"左右行错位：row" + i + " delta=" + delta.ToString("F2") + "px（left="
							+ lv[i].VisualTop.ToString("F2") + " right=" + rv[i].VisualTop.ToString("F2") + "）");
					}
					sb.AppendLine("aligned rows: " + compare + ", factor=" + left.Options.LineHeightFactor.ToString("F3")
						+ ", DefaultLineHeight=" + rtv.DefaultLineHeight.ToString("F2"));

					window.Close();
					return sb.ToString();
				}).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				report = (sbHolder[0]?.ToString() ?? "") + "\nOUTER EXCEPTION: " + ex;
			}
			System.IO.File.WriteAllText(ReportPath, report);
			Assert.DoesNotContain("OUTER EXCEPTION", report);
		}

		[Fact]
	public void CjkFallbackFontMetrics_FitDefaultLineSlot_SpacingNotInflated()
	{
		// v4.1.4 回归守卫（"FileDiff 行间距太宽"/"同区域 36 行 vs WPF 43 行"根因）：
		// AvaloniaEdit 行槽 = max(行自然高, DefaultTextHeight × LineHeightFactor)，CJK
		// 行自然高来自全局回退 Noto Sans CJK SC 的垂直度量。v4.1.2 时该度量 1.448em
		//（hhea；Win DWrite 下 1.619em）→ 13px 下 CJK 自然高 18.82/21.05px，且默认
		// factor 1.16 的槽也只有 17.66px，SideBySideLineHeightSynchronizer 被迫把含
		// 中文文件全部行槽抬到 CJK 自然高（行间距过宽）。v4.1.4 两步修复：CodeEditor
		// 置 LineHeightFactor=1.0（行槽 = ASCII 自然高，行密度对齐 WPF 3.13.2），子集
		// 字体度量收紧到 1.16em（hhea/OS/2 win/typo 三处一致，15.08px@13px）≤ 新槽。
		// 本测试守卫两个不变量（同环境实测，跨平台成立；走真实 CodeEditor VisualLine
		// 度量，与 TextView 排版同口径），若字体文件被替换回宽度量（或子集工具重建丢失
		// 度量修正）将在此失败而不是悄悄放大行距：
		//   1) CJK 自然行高 ≤ ASCII 自然行高（factor=1.0 时行槽即 ASCII 自然高）；
		//   2) DefaultLineHeight == ASCII 自然行高（factor=1.0 生效，无 1.16 放大——
		//      行密度守卫，回归"36 行"症状时在此红）。
		HeadlessAppBootstrap.EnsureStarted();
		Dispatcher.UIThread.InvokeAsync(delegate
		{
			var probe = new ForkPlus.UI.Controls.Editor.CodeEditor();
			probe.FontSize = 13.0;
			var window = new Window { Width = 400, Height = 200, Content = probe };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			try
			{
				double asciiHeight = MeasureNaturalLineHeight(probe, "x = 123;");
				double cjkHeight = MeasureNaturalLineHeight(probe, "// 中文注释行验证回退字体行高");
				Assert.True(asciiHeight > 0.0 && cjkHeight > 0.0,
					"行高测量失败：ascii=" + asciiHeight + " cjk=" + cjkHeight);
				// 1) CJK ≤ 行槽（factor=1.0 → 槽 = ASCII 自然高）
				Assert.True(cjkHeight <= asciiHeight + 0.01,
					"CJK 回退字体自然行高 " + cjkHeight.ToString("F2") + "px 超过默认行槽 " + asciiHeight.ToString("F2")
					+ "px（ASCII 自然高，factor=1.0 口径）——含中文 diff 行距会被放大"
					+ "（v4.1.4 度量收紧回弹？CJK 应 ≤ 1.16em，即 ≤15.08px@13px）");
				// 2) 行密度守卫：DefaultLineHeight 就是 ASCII 自然高（CodeEditor factor=1.0）
				double defaultLineHeight = probe.TextArea.TextView.DefaultLineHeight;
				Assert.True(Math.Abs(defaultLineHeight - asciiHeight) < 0.05,
					"DefaultLineHeight " + defaultLineHeight.ToString("F2") + "px ≠ ASCII 自然行高 "
					+ asciiHeight.ToString("F2") + "px——LineHeightFactor 应为 1.0（CodeEditor 构造设置），"
					+ "若放大 ≈16% 即『同区域 36 行 vs WPF 43 行』回归（factor 被改回 1.16？）");
			}
			finally
			{
				window.Close();
				Dispatcher.UIThread.RunJobs();
			}
		}).GetAwaiter().GetResult();
	}

	/// <summary>已挂树的 CodeEditor 排版一行文本，返回首行自然行高（TextLine.Height，
	/// 与 TextView.CalculateDefaultTextMetrics 同口径，走生产 CJK 回退链）。
	/// probe 编辑器复用同一实例连续测量（DefaultLineHeight 守卫需要它保持挂树）。</summary>
	private static double MeasureNaturalLineHeight(ForkPlus.UI.Controls.Editor.CodeEditor probe, string text)
	{
		probe.Text = text + "\n";
		Dispatcher.UIThread.RunJobs();
		return probe.TextArea.TextView.VisualLines[0].TextLines[0].Height;
	}

	[Fact]
	public void SideBySide_PureAscii_FactorStaysInitial_NoVisualChange()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var control = new SideBySideTextDiffControl();
				var window = new Window { Width = 900, Height = 400, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				control.SetDiff(MakeAddDiff(30, wide: false, withCjk: false), 4, entireFile: true, DiffLocation.Unstaged);
				Dispatcher.UIThread.RunJobs();
				try
				{
					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					// 纯 ASCII：最大自然行高 15.22 ≤ 初始 DefaultLineHeight（factor=1.0 →
				// 15.22px，v4.1.4 起 CodeEditor 构造即置 1.0），factor 必须保持初始值——
				// 无 CJK 文件行密度与 WPF 3.13.2 一致（同视口可见行数相同）。
				Assert.True(Math.Abs(left.Options.LineHeightFactor - right.Options.LineHeightFactor) < 0.001,
					"两侧 factor 应相等");
				Assert.True(left.Options.LineHeightFactor < 1.01,
					"纯 ASCII 文件 factor 不应被抬高（实际 " + left.Options.LineHeightFactor.ToString("F3")
					+ "，初始值应为 CodeEditor 构造设置的 1.0）");
				}
				finally
				{
					window.Close();
				}
			}).GetAwaiter().GetResult();
		}
	}
}
