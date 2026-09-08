// 探针5：复现"文件变更区域选中几行直接卡死"（问题3）。
// 用 headless 真实鼠标事件序列（hover → drag 选中 → release）驱动 SplitCommitTextDiffControl，
// 超时检测 + UI 线程堆栈抓取定位挂起点。
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class SelectionFreezeProbeTests
	{
		private static Diff MakeDiff()
		{
			// 3 context + 2 deleted + 3 added + 3 context（真实 git diff 每行带尾部 \n）
			var lines = new[]
			{
				"context line 1\n", // 0
				"context line 2\n", // 1
				"context line 3\n", // 2
				"deleted line 1\n", // 3
				"deleted line 2\n", // 4
				"added line 1\n",   // 5
				"added line 2\n",   // 6
				"added line 3\n",   // 7
				"context line 4\n", // 8
				"context line 5\n", // 9
				"context line 6\n"  // 10
			};
			// Range 语义是 (start, end) 下标开区间
			var subChunk = new SubChunk(
				new Range(0, 3),   // preContext: lines 0-2
				new Range(3, 5),   // deleted: lines 3-4
				new Range(5, 8),   // added: lines 5-7
				new Range(8, 11),  // postContext: lines 8-10
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(10, 6, 20, 7, null, new[] { subChunk });
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines, new[] { chunk }, null, Diff.FileType.Text, false);
		}

		// 超时版 runner：action 与"事后 marker job"都必须在时限内完成，否则报告挂起阶段
		private static string RunWithTimeout(Func<string> action, int timeoutMs)
		{
			HeadlessAppBootstrap.EnsureStarted();
			// 用 Func<Task<T>> 重载拿到真正的 Task<string>，才能 Wait(timeout)
			var task = Dispatcher.UIThread.InvokeAsync(async delegate { return action(); });
			if (!task.Wait(timeoutMs))
			{
				return "TIMEOUT(action): UI 线程在 action 内挂死";
			}
			// action 完成后，再发一个 Background 优先级 marker：若主循环陷入
			// 无限 layout/Background-job 循环，这个 marker 永远得不到执行。
			var marker = Dispatcher.UIThread.InvokeAsync(async delegate { return "alive"; }, DispatcherPriority.Background);
			if (!marker.Wait(timeoutMs))
			{
				return "TIMEOUT(marker/main-loop): 主循环陷入无限循环（posted job / layout）";
			}
			return task.Result;
		}

		[Fact]
		public void Probe_Selection_Freeze()
		{
			string report = RunWithTimeout(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					var diff = MakeDiff();
					var control = new SplitCommitTextDiffControl();
					var window = new Window { Width = 900, Height = 400, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					control.SetDiff(diff, 4, false, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();

					var editor = (CodeEditor)control.GetType().GetField("_editor",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(control);
					sb.AppendLine("editor type: " + editor.GetType().Name);
					sb.AppendLine("doc lineCount=" + editor.Document.LineCount + ", textLen=" + editor.Text.Length);
					sb.AppendLine("doc text: [" + editor.Text.Replace("\n", "\\n") + "]");
					sb.AppendLine("visualLine count: " + (editor.TextArea.TextView.VisualLines?.Count ?? -1));

					// --- 阶段1：程序化 Select（不经过鼠标） ---
					int selStart = editor.Text.IndexOf("deleted line 1", StringComparison.Ordinal);
					sb.AppendLine("selStart of 'deleted line 1' = " + selStart);
					if (selStart >= 0)
					{
						editor.Select(selStart, 25);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine("stage1 programmatic select OK, selLength=" + editor.SelectionLength);
					}

					// --- 强制渲染一帧：Render(DiffSelectionLayer) 只在真实渲染时执行，
					//     DrawChunk → ShowChunkAdorner → AdornerLayer.GetAdornerLayer（替换窗口内容！）都在渲染期间发生 ---
					HeadlessWindowExtensions.CaptureRenderedFrame(window);
					sb.AppendLine("frame1 captured, window content = " + window.Content?.GetType().Name);

					// --- 阶段2：真实鼠标 hover 到 hunk 上 ---
					// 找 deleted line 1 的可视位置
					var vp1 = editor.TextArea.TextView.GetVisualLine(4); // 第4行 = deleted line 1（1-based）
					if (vp1 != null)
					{
						var pos = new Point(200, vp1.VisualTop - editor.TextArea.TextView.ScrollOffset.Y + 5);
						HeadlessWindowExtensions.MouseMove(window, pos, Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						sb.AppendLine("stage2 mouse hover OK at " + pos + ", window content = " + window.Content?.GetType().Name);
					}

					// --- 阶段3：拖选 2-3 行 ---
					var vlStart = editor.TextArea.TextView.GetVisualLine(3);
					var vlEnd = editor.TextArea.TextView.GetVisualLine(6);
					if (vlStart != null && vlEnd != null)
					{
						double y1 = vlStart.VisualTop - editor.TextArea.TextView.ScrollOffset.Y + 6;
						double y2 = vlEnd.VisualTop - editor.TextArea.TextView.ScrollOffset.Y + 6;
						HeadlessWindowExtensions.MouseDown(window, new Point(250, y1), Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
						for (double y = y1; y <= y2; y += 4)
						{
							HeadlessWindowExtensions.MouseMove(window, new Point(250, y), Avalonia.Input.RawInputModifiers.None);
						}
						HeadlessWindowExtensions.MouseUp(window, new Point(250, y2), Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine("stage3 drag select OK, selLength=" + editor.SelectionLength);
					}

					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
				}
				return sb.ToString();
			}, 30000);
			System.IO.File.WriteAllText("/tmp/selection_freeze_probe.txt", report);
			Assert.True(true, report);
		}
	}
}
