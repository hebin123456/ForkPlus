// 探针（2026-09-16，"FileDiff hover 自动选中的对比块区域偏了 + 鼠标不在块上仍显示浮窗"）：
// 用 headless 真实鼠标逐行 hover 驱动 SplitCommitTextDiffControl（Split 视图、非 entire-file，
// 即带 @@ 头的标准 hunk 视图），逐行记录：
//   - 鼠标所在文档行号/文本
//   - ActiveChunk（hover 命中的 hunk）及其覆盖的文档行区间
//   - 悬浮按钮 Adorner 是否存在（鼠标不在 hunk 上时必须不存在）
// 期望（正确行为）：hover 命中 hunk 的行区间必须包含鼠标所在行；不在任何 hunk 上时
// ActiveChunk 必须为 null 且 Adorner 必须被移除。
// 结构：2 个 chunk（2 个 @@ 头），每 chunk：3 pre-ctx + 1 deleted + 1 added + 5 post-ctx。
// 按 ReadCustomHunk（±2 ctx）推得期望 hunk：chunk1 → doc 3..8，chunk2 → doc 14..19。
using System;
using System.Linq;
using System.Reflection;
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
	public class FileDiffHoverChunkMappingProbeTests
	{
		private static Diff MakeDiff()
		{
			var lines = new[]
			{
				"c1 ctx1\n", // 0
				"c1 ctx2\n", // 1
				"c1 ctx3\n", // 2
				"c1 deleted\n", // 3
				"c1 added\n", // 4
				"c1 ctx4\n", // 5
				"c1 ctx5\n", // 6
				"c1 ctx6\n", // 7
				"c1 ctx7\n", // 8
				"c1 ctx8\n", // 9
				"c2 ctx1\n", // 10
				"c2 ctx2\n", // 11
				"c2 ctx3\n", // 12
				"c2 deleted\n", // 13
				"c2 added\n", // 14
				"c2 ctx4\n", // 15
				"c2 ctx5\n", // 16
				"c2 ctx6\n", // 17
				"c2 ctx7\n", // 18
				"c2 ctx8\n"  // 19
			};
			var subChunk1 = new SubChunk(
				new Range(0, 3), new Range(3, 4), new Range(4, 5), new Range(5, 10),
				NoNewLineAtEndOfFile.None);
			var subChunk2 = new SubChunk(
				new Range(10, 13), new Range(13, 14), new Range(14, 15), new Range(15, 20),
				NoNewLineAtEndOfFile.None);
			var chunk1 = new Chunk(10, 6, 20, 7, null, new[] { subChunk1 });
			var chunk2 = new Chunk(30, 6, 40, 7, null, new[] { subChunk2 });
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines,
				new[] { chunk1, chunk2 }, null, Diff.FileType.Text, false);
		}

		private static string RunWithTimeout(Func<string> action, int timeoutMs)
		{
			HeadlessAppBootstrap.EnsureStarted();
			var task = Dispatcher.UIThread.InvokeAsync(async delegate { return action(); });
			if (!task.Wait(timeoutMs))
			{
				return "TIMEOUT(action): UI 线程在 action 内挂死";
			}
			var marker = Dispatcher.UIThread.InvokeAsync(async delegate { return "alive"; }, DispatcherPriority.Background);
			if (!marker.Wait(timeoutMs))
			{
				return "TIMEOUT(marker/main-loop): 主循环陷入无限循环";
			}
			return task.Result;
		}

		private static object GetLayer(CommitCodeEditor editor)
		{
			FieldInfo f = typeof(CommitCodeEditor).GetField("_diffSelectionLayer",
				BindingFlags.NonPublic | BindingFlags.Instance);
			return f.GetValue(editor);
		}

		private static object GetAdorner(object layer)
		{
			FieldInfo f = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_adorner",
				BindingFlags.NonPublic | BindingFlags.Instance);
			return f.GetValue(layer);
		}

		private static string DescribeChunk(CommitDiffSelectedRange r)
		{
			if (r == null)
			{
				return "null";
			}
			var range = r.VisualChunk.CustomHunks[r.CustomHunkIndex];
			int first = r.VisualChunk.VisualLines[range.Start].LineNumber + 1;
			int last = r.VisualChunk.VisualLines[range.End - 1].LineNumber + 1;
			return "hunk#" + r.CustomHunkIndex + " docLines " + first + ".." + last;
		}

		[Fact]
		public void Probe_Hover_Chunk_Mapping()
		{
			string report = RunWithTimeout(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					var diff = MakeDiff();
					var control = new SplitCommitTextDiffControl();
					var window = new Window { Width = 900, Height = 600, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// EntireFile=false：非 entire-file 模式（带 @@ 头）
					control.SetDiff(diff, 4, false, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

					var editor = (CommitCodeEditor)control.GetType().GetField("_editor",
						BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control);
					var layer = GetLayer(editor);
					var textView = editor.TextArea.TextView;

					sb.AppendLine("doc lineCount=" + editor.Document.LineCount);
					for (int i = 1; i <= editor.Document.LineCount; i++)
					{
						var docLine = editor.Document.GetLineByNumber(i);
						sb.AppendLine("docline " + i + ": [" + editor.Document.GetText(docLine.Offset, docLine.Length).TrimEnd() + "]");
					}

					// TextView 原点在窗口中的位置（鼠标坐标换算）
					var origin = textView.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
					sb.AppendLine("textView origin in window: " + origin + ", scrollOffset=" + textView.ScrollOffset);

					// 逐行 hover（只遍历当前可视行）
					for (int line = 1; line <= editor.Document.LineCount; line++)
					{
						var vl = textView.GetVisualLine(line);
						if (vl == null)
						{
							sb.AppendLine("line " + line + ": NOT VISUALIZED");
							continue;
						}
						double yInTextView = vl.VisualTop - textView.ScrollOffset.Y + vl.Height / 2;
						var pos = new Point(origin.X + 250, origin.Y + yInTextView);
						HeadlessWindowExtensions.MouseMove(window, pos, global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

						var active = editor.ActiveChunk;
						var adorner = GetAdorner(layer);
						var docLine = editor.Document.GetLineByNumber(line);
						string text = editor.Document.GetText(docLine.Offset, docLine.Length).TrimEnd();
						sb.AppendLine("hover line " + line + " [" + text + "] → active=" + DescribeChunk(active)
							+ ", adorner=" + (adorner != null));
					}

					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
				}
				return sb.ToString();
			}, 60000);
			System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hover_chunk_probe.txt"), report);
			// 探针阶段：只报告不硬断言（探明实际行为后再补回归断言）
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
		}
	}
}
