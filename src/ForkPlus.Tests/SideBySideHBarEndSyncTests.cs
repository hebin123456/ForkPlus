// 回归测试（2026-09-15 第二轮，"两水平滚动条拖到末端即失步"修复产物；
// 第三轮并入"真实 Track 拖拽后 thumb 冻结"场景）：
// 根因（探针实证）：左编辑器竖滚动条 Hidden、右侧可见 → 两侧内容区（viewport）宽差
// = 竖滚动条宽（13px）→ ScrollBarMaximum.X = Extent − Viewport 两侧不等（1172.7 vs
// 1185.7）→ 拖窄视口侧 thumb 到末端时同步写宽侧的目标偏移被钳到更小 max → 两栏视图
// 永久错位 13px；两 thumb Maximum 不等也让末端位置视觉不齐（用户补拖另一侧即真失步）。
// 修复：SideBySideExtentSynchronizer 给宽视口侧 extent 补偿视口差，两侧
// ScrollBarMaximum 恒等（宽侧可滚入等量空白，VS Code 同款），全区间像素同步零钳制。
// 本测试守卫：末端拖拽（双向）、末端附近交替拖、滚轮+中途拖，两侧视图偏移与
// 滚动条 Value/Maximum 恒等。
//
// 第三轮根因（"拖过 thumb 后该滚动条视图动 thumb 不动"，真实拖拽路径）：
// Thumb.DragDelta → Track.ApplyThumbDrag → 模板里 Track 的
// `Value="{Binding Value, RelativeSource TemplatedParent, TwoWay}"` 回写 ScrollBar.Value——
// 普通 {Binding} 回写经 AvaloniaPropertyAccessorNode.WriteValueToSource 用 SetValue
//（LocalValue 优先级），一次真实拖拽即永久压死 ScrollBar.AttachToScrollViewer 建立的
// Template 优先级 Value←Offset 绑定 → 此后程序化 Offset 写入（SideBySide 同步、滚轮）
// 只动视图不动 thumb；拖过哪侧 thumb 哪侧冻结。修复：Scrollviewer.axaml 4 处 Track
// Value 改 `{TemplateBinding Value, Mode=TwoWay}`（Fluent 原生，回写 SetCurrentValue 绑定安全）。
// 真实拖拽须反射调 Track.ApplyThumbDrag 模拟（直接 SetCurrentValue(ScrollBar.Value)
// 绕过 Track，复现不了）；守卫断言：真实拖拽 + 程序化复位后 thumb 仍跟随视图。
using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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
	public class SideBySideHBarEndSyncTests
	{
		private const string ReportPath = @"C:\Users\H00518~1\AppData\Local\Temp\opencode\sxs_hbar_regression.txt";

		private static ScrollViewer GetSv(DiffCodeEditor editor)
		{
			return editor.GetVisualDescendants().OfType<ScrollViewer>().First((ScrollViewer x) => x.Name == "PART_ScrollViewer");
		}

		private static ScrollBar GetNamedBar(ScrollViewer sv, string name)
		{
			return sv.GetVisualDescendants().OfType<ScrollBar>().First((ScrollBar b) => b.Name == name);
		}

		private static Diff MakeAddDiff(int rows)
		{
			var lines = new System.Collections.Generic.List<string>();
			for (int i = 0; i < rows; i++)
			{
				lines.Add((i % 5 == 0)
					? "// 中文注释行第" + i + "行验证回退字体行高" + new string('x', 180) + "\n"
					: "export function helper" + i + "() { return " + i + "; } " + new string('y', 180) + "\n");
			}
			var subChunk = new SubChunk(
				new Range(0, 0), new Range(0, 0),
				new Range(0, lines.Count), new Range(lines.Count, lines.Count),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(0, 0, 1, lines.Count, null, new[] { subChunk });
			return new Diff("new.ts", "new.ts", null, null, "111", "222", lines.ToArray(), new[] { chunk }, null, Diff.FileType.Text, false);
		}

		// 模拟用户真实拖 thumb：反射调 Track.ApplyThumbDrag（Thumb.DragDelta 的内部落点，
		// 会经模板绑定回写 ScrollBar.Value——第三轮修复的触发路径）
		private static void DragTrack(ScrollBar bar, double pixelDeltaX, double pixelDeltaY)
		{
			Track track = bar.GetVisualDescendants().OfType<Track>().First();
			var method = typeof(Track).GetMethod("ApplyThumbDrag",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			Assert.True(method != null, "Track.ApplyThumbDrag 反射失败");
			method.Invoke(track, new object[] { new VectorEventArgs { Vector = new Vector(pixelDeltaX, pixelDeltaY) } });
		}

		// 第三轮守卫：真实 Track 拖拽（模板 Value 绑定回写路径）之后，
		// 程序化 Offset 复位时 thumb 必须跟随视图（修复前回写 SetValue 杀死
		// Template 优先级绑定 → thumb 冻结在旧值）。
		[Fact]
		public void SideBySide_RealTrackDrag_ThumbFollowsViewAfterProgrammaticScroll()
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
					control.SetDiff(MakeAddDiff(60), 4, entireFile: true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();

					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					TextView ltv = left.TextArea.TextView;
					TextView rtv = right.TextArea.TextView;
					ScrollViewer leftSv = GetSv(left);
					ScrollBar leftHBar = GetNamedBar(leftSv, "PART_HorizontalScrollBar");
					ScrollBar rightHBar = GetNamedBar(GetSv(right), "PART_HorizontalScrollBar");

					// 断言 + 记录：thumb 值 == 自己视图偏移（thumb 跟随视图）、两侧视图/Value 恒等
					Action<string> assertThumbFollows = delegate (string tag)
					{
						sb.AppendLine(tag
							+ ": L.viewX=" + ltv.ScrollOffset.X.ToString("F1") + " R.viewX=" + rtv.ScrollOffset.X.ToString("F1")
							+ " | L.hbar=" + leftHBar.Value.ToString("F1") + "/" + leftHBar.Maximum.ToString("F1")
							+ " R.hbar=" + rightHBar.Value.ToString("F1") + "/" + rightHBar.Maximum.ToString("F1"));
						Assert.True(Math.Abs(leftHBar.Value - ltv.ScrollOffset.X) <= 0.5,
							tag + "：左 thumb 未跟随视图（bar=" + leftHBar.Value.ToString("F1")
							+ " view=" + ltv.ScrollOffset.X.ToString("F1") + "）——Track 回写压死了 Value←Offset 绑定");
						Assert.True(Math.Abs(rightHBar.Value - rtv.ScrollOffset.X) <= 0.5,
							tag + "：右 thumb 未跟随视图（bar=" + rightHBar.Value.ToString("F1")
							+ " view=" + rtv.ScrollOffset.X.ToString("F1") + "）——Track 回写压死了 Value←Offset 绑定");
						Assert.True(Math.Abs(ltv.ScrollOffset.X - rtv.ScrollOffset.X) <= 0.5,
							tag + "：两栏视图水平偏移失步（L=" + ltv.ScrollOffset.X.ToString("F1")
							+ " R=" + rtv.ScrollOffset.X.ToString("F1") + "）");
						Assert.True(Math.Abs(leftHBar.Value - rightHBar.Value) <= 0.5,
							tag + "：两 thumb Value 失步（L=" + leftHBar.Value.ToString("F1")
							+ " R=" + rightHBar.Value.ToString("F1") + "）");
					};

					// 1) 真实拖左 thumb 到末端（经 Track.ApplyThumbDrag——模板绑定回写路径）。
					DragTrack(leftHBar, 100000.0, 0.0);
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs();
					assertThumbFollows("real drag left to end");

					// 2) 程序化复位 Offset（不经 Track——滚轮/同步链路径）：修复前左 thumb
					//    冻结在末端（L.hbar 卡 1185.7 而视图归 0）。
					leftSv.Offset = leftSv.Offset.WithX(0.0);
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs();
					assertThumbFollows("programmatic reset to 0");

					// 3) 再真实拖左 thumb（中部）：拖拽应生效（视图前移）且不失步。
					DragTrack(leftHBar, 200.0, 0.0);
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs();
					Assert.True(ltv.ScrollOffset.X > 1.0, "真实拖左 200px 后左视图应前移（实际 "
						+ ltv.ScrollOffset.X.ToString("F1") + "——thumb 可能冻结在末端未归零）");
					assertThumbFollows("real drag left by 200px");

					// 4) 真实拖右 thumb（模板回写杀右绑定路径）后，程序化复位再验证两侧。
					DragTrack(rightHBar, 150.0, 0.0);
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs();
					assertThumbFollows("real drag right by 150px");

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
		public void SideBySide_DragThumbToEnd_ViewsAndBarsStaySynced()
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
					control.SetDiff(MakeAddDiff(60), 4, entireFile: true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();

					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					TextView ltv = left.TextArea.TextView;
					TextView rtv = right.TextArea.TextView;
					ScrollViewer leftSv = GetSv(left);
					ScrollViewer rightSv = GetSv(right);
					ScrollBar leftHBar = GetNamedBar(leftSv, "PART_HorizontalScrollBar");
					ScrollBar rightHBar = GetNamedBar(rightSv, "PART_HorizontalScrollBar");
					ScrollBar rightVBar = GetNamedBar(rightSv, "PART_VerticalScrollBar");

					// 断言 + 记录：两侧视图偏移、滚动条 Value/Maximum 恒等（±0.5px）
					Action<string> assertSynced = delegate (string tag)
					{
						sb.AppendLine(tag
							+ ": L.viewX=" + ltv.ScrollOffset.X.ToString("F1") + " R.viewX=" + rtv.ScrollOffset.X.ToString("F1")
							+ " | L.hbar=" + leftHBar.Value.ToString("F1") + "/" + leftHBar.Maximum.ToString("F1")
							+ " R.hbar=" + rightHBar.Value.ToString("F1") + "/" + rightHBar.Maximum.ToString("F1"));
						Assert.True(Math.Abs(ltv.ScrollOffset.X - rtv.ScrollOffset.X) <= 0.5,
							tag + "：两栏视图水平偏移失步（L=" + ltv.ScrollOffset.X.ToString("F1")
							+ " R=" + rtv.ScrollOffset.X.ToString("F1") + "）");
						Assert.True(Math.Abs(leftHBar.Value - rightHBar.Value) <= 0.5,
							tag + "：两 thumb Value 失步（L=" + leftHBar.Value.ToString("F1")
							+ " R=" + rightHBar.Value.ToString("F1") + "）");
					};

					// 0) 前置：右侧竖滚动条可见（两侧视口宽差 = 竖滚动条宽，即用户实际场景），
					//    修复后两侧 ScrollBarMaximum 应恒等（ExtentSynchronizer 视口补偿）。
					Assert.True(rightVBar != null && rightVBar.IsVisible,
						"前置失败：右侧竖滚动条应可见（复现视口宽差场景）");
					Assert.True(Math.Abs(leftSv.Viewport.Width - rightSv.Viewport.Width) > 1.0,
						"前置失败：两侧视口应有宽差（左 vbar Hidden / 右可见）");
					Assert.True(Math.Abs(leftSv.ScrollBarMaximum.X - rightSv.ScrollBarMaximum.X) <= 0.5,
						"两侧 ScrollBarMaximum.X 应恒等（视口补偿后）：L=" + leftSv.ScrollBarMaximum.X.ToString("F1")
						+ " R=" + rightSv.ScrollBarMaximum.X.ToString("F1"));
					sb.AppendLine("init: L.viewport=" + leftSv.Viewport.Width.ToString("F1")
						+ " R.viewport=" + rightSv.Viewport.Width.ToString("F1")
						+ " svMax=" + leftSv.ScrollBarMaximum.X.ToString("F1") + "/" + rightSv.ScrollBarMaximum.X.ToString("F1"));

					// 1) 模拟用户拖右栏 thumb 到末端（Track 路径 = SetCurrentValue(Value, Maximum)）。
					//    修复前：左被钳到更小 max → 两栏视图永久错位 13px（desync=True）。
					rightHBar.SetCurrentValue(RangeBase.ValueProperty, rightHBar.Maximum);
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs();
					assertSynced("drag right to max");

					// 2) 复位后拖左栏 thumb 到末端（修复前右 thumb 到不了头，视觉不齐）。
					leftHBar.SetCurrentValue(RangeBase.ValueProperty, 0.0);
					Dispatcher.UIThread.RunJobs();
					leftHBar.SetCurrentValue(RangeBase.ValueProperty, leftHBar.Maximum);
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs();
					assertSynced("drag left to max");

					// 3) 复位后末端附近交替拖（用户"一不小心"场景：两侧轮流拖到接近末端）。
					leftHBar.SetCurrentValue(RangeBase.ValueProperty, 0.0);
					Dispatcher.UIThread.RunJobs();
					rightHBar.SetCurrentValue(RangeBase.ValueProperty, rightHBar.Maximum - 5.0);
					Dispatcher.UIThread.RunJobs();
					leftHBar.SetCurrentValue(RangeBase.ValueProperty, leftHBar.Maximum - 3.0);
					Dispatcher.UIThread.RunJobs();
					assertSynced("alternating near max");

					// 4) 复位后滚轮竖滚（右栏）再拖左 thumb 到中部（混合交互回归）。
					leftHBar.SetCurrentValue(RangeBase.ValueProperty, 0.0);
					Dispatcher.UIThread.RunJobs();
					var scrollBy = typeof(TouchpadAwareScrollViewer).GetMethod("ScrollBy",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					scrollBy.Invoke(rightSv, new object[] { 0.0, 96.0 });
					Dispatcher.UIThread.RunJobs();
					leftHBar.SetCurrentValue(RangeBase.ValueProperty, leftHBar.Maximum * 0.5);
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs();
					assertSynced("wheel + mid drag");

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
	}
}
