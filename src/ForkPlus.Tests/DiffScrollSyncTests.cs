// 回归测试（2026-09-03，"FileDiff 视图左右代码不同步滚动"修复产物）：
// 根因：AvaloniaEdit 12.x 的 TextEditor.ScrollToVerticalOffset/ScrollToHorizontalOffset
// 是空操作（源码滚动实现整段被注释），兼容层 ScrollViewerCompat 直接转发 → SideBySide
// 视图左右面板各滚各的；CodeEditor.SetScrollPosition（切换文件恢复滚动位置）也失效。
// 修复：兼容层改为经模板 PART_ScrollViewer（TouchpadAwareScrollViewer）的 Offset 滚动，
// 与 AvaloniaEdit 自家 ScrollTo/滚轮路径一致。
// 本测试守卫两条防线：
//   1) ScrollToVerticalOffsetCompat 能真正滚动编辑器并触发 ScrollOffsetChanged；
//   2) 真实 SideBySideTextDiffControl：滚右侧 → 左侧跟随；滚左侧 → 右侧跟随（用户报告场景）。
using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiffScrollSyncTests
	{
		// 300 行的文本 diff：左右两侧各约 300 行（约 5700px），远超 400px 视口，
		// 保证 250px 的滚动量在两侧文档范围内（IsVerticalOffsetWithinDocumentArea 才会放行）。
		private static Diff MakeLongDiff()
		{
			var lines = new string[300];
			for (int i = 0; i < 300; i++)
			{
				lines[i] = "line " + (i + 1).ToString().PadLeft(3, '0') + " some padding text to make lines\n";
			}
			// 在第 4/5 行做一处删除+新增（added < deleted，New 侧补 1 条对齐空行，两侧等高）。
			lines[3] = "old line A\n";
			lines[4] = "old line B\n";
			lines[5] = "new line A\n";
			var subChunk = new SubChunk(
				new Range(0, 3),
				new Range(3, 5),
				new Range(5, 6),
				new Range(6, 300),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(1, 299, 1, 299, null, new[] { subChunk });
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines, new[] { chunk }, null, Diff.FileType.Text, false);
		}

		[Fact]
		public void ScrollToVerticalOffsetCompat_ScrollsEditorAndRaisesEvent()
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
					var editor = new CodeEditor();
					editor.Text = string.Concat(Enumerable.Repeat("filler line\n", 400));
					var window = new Window { Width = 800, Height = 400, Content = editor };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// 诊断记录：AvaloniaEdit 原生 ScrollToVerticalOffset 是否为空操作。
					// 不作断言——若上游修复，此探针仅作信息输出，兼容层行为不受影响。
					editor.ScrollToVerticalOffset(300.0);
					Dispatcher.UIThread.RunJobs();
					double afterNative = editor.TextArea.TextView.ScrollOffset.Y;
					sb.AppendLine("native ScrollToVerticalOffset(300) -> offset=" + afterNative.ToString("F1") + " (0 = AvaloniaEdit no-op)");

					// 修复主体：兼容方法经 PART_ScrollViewer.Offset 真正滚动。
					int events = 0;
					editor.TextArea.TextView.ScrollOffsetChanged += delegate { events++; };
					editor.ScrollToVerticalOffsetCompat(300.0);
					Dispatcher.UIThread.RunJobs();
					double afterCompat = editor.TextArea.TextView.ScrollOffset.Y;
					sb.AppendLine("compat ScrollToVerticalOffsetCompat(300) -> offset=" + afterCompat.ToString("F1") + ", events=" + events);

					window.Close();
					Assert.True(Math.Abs(afterCompat - 300.0) < 1.5,
						"ScrollToVerticalOffsetCompat 未生效：滚动后 offset=" + afterCompat.ToString("F1") + "（期望 ~300）");
					Assert.True(events > 0, "滚动后应触发 ScrollOffsetChanged（左右同步的事件源）");
					return sb.ToString();
				}).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				report = (sbHolder[0]?.ToString() ?? "") + "\nOUTER EXCEPTION: " + ex;
			}
			System.IO.File.WriteAllText("/tmp/diff_scroll_sync_unit.txt", report);
			Assert.DoesNotContain("OUTER EXCEPTION", report);
		}

		[Fact]
		public void SideBySideTextDiffControl_ScrollSyncsLeftAndRight()
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
					// 用户报告场景的真实控件：FileDiff 视图 SideBySide 模式。
					var control = new SideBySideTextDiffControl();
					var window = new Window { Width = 900, Height = 400, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.SetDiff(MakeLongDiff(), 4, entireFile: true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();

					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					sb.AppendLine("lines: left=" + left.TextArea.TextView.Document.LineCount
						+ ", right=" + right.TextArea.TextView.Document.LineCount);

					// 模拟用户滚右侧：与 TouchpadAwareScrollViewer 滚轮路径等价（直接改 Offset）。
					ScrollViewer rightSv = right.GetVisualDescendants().OfType<ScrollViewer>()
						.First((ScrollViewer x) => x.Name == "PART_ScrollViewer");
					rightSv.Offset = rightSv.Offset.WithY(250.0);
					Dispatcher.UIThread.RunJobs();
					double leftAfterRightScroll = left.TextArea.TextView.ScrollOffset.Y;
					double rightAfterRightScroll = right.TextArea.TextView.ScrollOffset.Y;
					sb.AppendLine("scroll right 250 -> left=" + leftAfterRightScroll.ToString("F1")
						+ ", right=" + rightAfterRightScroll.ToString("F1"));

					// 反向：滚左侧 → 右侧跟随。原版（WPF 同款）防回声守卫会抑制
					// 100ms 内的"另一侧"滚动事件，模拟真人换面板的节奏等 150ms 再滚。
					System.Threading.Thread.Sleep(150);
					ScrollViewer leftSv = left.GetVisualDescendants().OfType<ScrollViewer>()
						.First((ScrollViewer x) => x.Name == "PART_ScrollViewer");
					leftSv.Offset = leftSv.Offset.WithY(400.0);
					Dispatcher.UIThread.RunJobs();
					double rightAfterLeftScroll = right.TextArea.TextView.ScrollOffset.Y;
					double leftAfterLeftScroll = left.TextArea.TextView.ScrollOffset.Y;
					sb.AppendLine("scroll left 400 -> left=" + leftAfterLeftScroll.ToString("F1")
						+ ", right=" + rightAfterLeftScroll.ToString("F1"));

					window.Close();
					Assert.True(Math.Abs(leftAfterRightScroll - 250.0) < 1.5,
						"滚右侧后左侧未同步：left=" + leftAfterRightScroll.ToString("F1") + "（期望 ~250）");
					Assert.True(Math.Abs(rightAfterLeftScroll - 400.0) < 1.5,
						"滚左侧后右侧未同步：right=" + rightAfterLeftScroll.ToString("F1") + "（期望 ~400）");
					return sb.ToString();
				}).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				report = (sbHolder[0]?.ToString() ?? "") + "\nOUTER EXCEPTION: " + ex;
			}
			System.IO.File.WriteAllText("/tmp/diff_scroll_sync_e2e.txt", report);
			Assert.DoesNotContain("OUTER EXCEPTION", report);
		}
	}
}
