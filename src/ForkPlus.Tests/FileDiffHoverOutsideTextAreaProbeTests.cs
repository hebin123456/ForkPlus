// 探针 v3（2026-09-16，"鼠标不在块的区域上面，有时候也会显示浮窗"）：
// v1/v2 证明文本区内 offset→hunk→rect 映射一致。本探针专门测"文本区之外"的命中：
//   - 左侧 52px 竖条（TextView 原点 X=52，怀疑是滚动条映射 minimap）
//   - 右侧竖直滚动条区域
//   - 文档末行以下的空白区
//   - 行尾右侧空白
// 期望（正确行为）：这些区域 hover 时 ActiveChunk 必须为 null、无浮窗；
// 若 GetPositionFromPoint 对区外坐标做钳制（映射到最近行/列）就会误激活 hunk。
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
	public class FileDiffHoverOutsideTextAreaProbeTests
	{
		private static Diff MakeDiff()
		{
			var lines = new string[]
			{
				"c1 ctx1\n", // 0
				"c1 ctx2\n", // 1
				"c1 ctx3\n", // 2
				"c1 deleted\n", // 3
				"c1 added\n", // 4
				"c1 post1\n", // 5
				"c1 post2\n", // 6
				"c1 post3\n", // 7
				"c1 post4\n", // 8
				"c1 post5\n", // 9
				"c2 ctx1\n", // 10
				"c2 ctx2\n", // 11
				"c2 ctx3\n", // 12
				"c2 deleted\n", // 13
				"c2 added\n", // 14
				"c2 post1\n", // 15
				"c2 post2\n", // 16
				"c2 post3\n", // 17
				"c2 post4\n", // 18
				"c2 post5\n"  // 19
			};
			var sub1 = new SubChunk(new Range(0, 3), new Range(3, 4), new Range(4, 5), new Range(5, 10),
				NoNewLineAtEndOfFile.None);
			var sub2 = new SubChunk(new Range(10, 13), new Range(13, 14), new Range(14, 15), new Range(15, 20),
				NoNewLineAtEndOfFile.None);
			var chunk1 = new Chunk(10, 6, 20, 7, null, new[] { sub1 });
			var chunk2 = new Chunk(30, 6, 40, 7, null, new[] { sub2 });
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines,
				new[] { chunk1, chunk2 }, null, Diff.FileType.Text, false);
		}

		private static string RunWithTimeout(Func<string> action, int timeoutMs)
		{
			HeadlessAppBootstrap.EnsureStarted();
			var task = Dispatcher.UIThread.InvokeAsync(async delegate { return action(); });
			if (!task.Wait(timeoutMs))
			{
				return "TIMEOUT(action)";
			}
			var marker = Dispatcher.UIThread.InvokeAsync(async delegate { return "alive"; }, DispatcherPriority.Background);
			if (!marker.Wait(timeoutMs))
			{
				return "TIMEOUT(marker)";
			}
			return task.Result;
		}

		private static object GetAdorner(object layer)
		{
			return typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_adorner",
				BindingFlags.NonPublic | BindingFlags.Instance).GetValue(layer);
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
		public void Probe_Hover_Outside_TextArea()
		{
			string report = RunWithTimeout(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					var control = new SplitCommitTextDiffControl();
					var window = new Window { Width = 900, Height = 600, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.SetDiff(MakeDiff(), 4, false, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

					var editor = (CommitCodeEditor)control.GetType().GetField("_editor",
						BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control);
					var layer = typeof(CommitCodeEditor).GetField("_diffSelectionLayer",
						BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);
					var tv = editor.TextArea.TextView;
					var tvOrigin = tv.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
					var editorBounds = editor.Bounds;
					sb.AppendLine("editor bounds in window: " + editor.TranslatePoint(new Point(0, 0), window)
						+ " size=" + editorBounds.Size);
					sb.AppendLine("textView origin: " + tvOrigin + " size=" + tv.Bounds.Size);
					sb.AppendLine("editor width=" + editorBounds.Width + " height=" + editorBounds.Height);
					sb.AppendLine("doc lineCount=" + editor.Document.LineCount);

					// helper：hover 窗口坐标并报告
					Action<string, Point> hover = (label, p) =>
					{
						HeadlessWindowExtensions.MouseMove(window, p, global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var active = editor.ActiveChunk;
						sb.AppendLine(label + " at " + p + " → active=" + DescribeChunk(active)
							+ " adorner=" + (GetAdorner(layer) != null));
					};

					// 文本区内基线：hover 第5行（c1 deleted，hunk 内）
					var vl5 = tv.GetVisualLine(5);
					double yDel = vl5.VisualTop - tv.ScrollOffset.Y + vl5.Height / 2;
					hover("baseline(text,hunk-del)", new Point(tvOrigin.X + 100, tvOrigin.Y + yDel));
					// 文本区内：hover 第9行（c1 post3，hunk 外的 context）
					var vl9 = tv.GetVisualLine(9);
					double yCtx = vl9.VisualTop - tv.ScrollOffset.Y + vl9.Height / 2;
					hover("baseline(text,ctx-out-of-hunk)", new Point(tvOrigin.X + 100, tvOrigin.Y + yCtx));

					// 左侧竖条（X 在 0..tvOrigin.X 区间）：逐档
					foreach (double dx in new double[] { 2, 15, 30, 45 })
					{
						hover("leftStrip(dx=" + dx + ",row=del)", new Point(dx, tvOrigin.Y + yDel));
					}
					// 左侧竖条但 Y 在 hunk 外 context 行
					hover("leftStrip(row=ctx-out)", new Point(20, tvOrigin.Y + yCtx));

					// 右侧滚动条区域（编辑器右缘）
					double rightEdge = editorBounds.Width - 6;
					hover("rightScrollbar(row=ctx-out)", new Point(rightEdge, tvOrigin.Y + yCtx));
					hover("rightScrollbar(row=del)", new Point(rightEdge, tvOrigin.Y + yDel));

					// 文档末行以下的空白区（bottom 空白）
					var lastLine = tv.GetVisualLine(editor.Document.LineCount - 1);
					double yBelowDoc = lastLine.VisualTop - tv.ScrollOffset.Y + lastLine.Height + 30;
					if (yBelowDoc < editorBounds.Height - 4)
					{
						hover("belowDocument(center-x)", new Point(tvOrigin.X + 100, yBelowDoc));
						hover("belowDocument(left-x)", new Point(20, yBelowDoc));
						hover("belowDocument(right-x)", new Point(rightEdge, yBelowDoc));
					}
					else
					{
						sb.AppendLine("belowDocument: 窗口太矮无空白区（yBelowDoc=" + yBelowDoc + "）");
					}

					// 顶部边缘之上（Y<0 不可达，用 TextView 上边缘附近的行首）
					hover("topEdge(x=leftStrip,y=2)", new Point(20, 2));

					// 行尾右侧空白（同一行 y，X 超出该行文本长度但在 TextView 内）
					hover("rightOfText(row=ctx-out)", new Point(tvOrigin.X + tv.Bounds.Width - 20, tvOrigin.Y + yCtx));

					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
				}
				return sb.ToString();
			}, 60000);
			System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hover_outside_probe.txt"), report);
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
		}
	}
}
