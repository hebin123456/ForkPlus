// 回归测试（2026-09-16，"hover 高亮区域偏移 / 鼠标不在块上仍显示浮窗"）：
// 探针 v3（FileDiffHoverOutsideTextAreaProbeTests）证明：鼠标悬停在 TextView 之外
// （左侧滚动条/变更映射条 0..52px）且 Y 与 hunk 行对齐时，AvaloniaEdit 12 的
// GetPositionFromPoint 会把区外坐标钳制到最近行（WPF 原版返回 null），导致
// hunk 被激活并弹出 Stage/Discard 浮窗；映射条色块按全文档比例缩放定位，与
// 实际文本行错位，故高亮落在未变化的行上。修复（ChunkSelectionLayer.GetChunkUnderMousePointer
// 增加 TextView 边界守卫）后本测试锁定行为：
//   - 文本区内 hover hunk 行 → 激活 + 浮窗（保持不变）
//   - 文本区外（左条/滚动条/文档下方/顶缘）hover → active=null 且无浮窗
//   - TextView 内行尾右侧（past-EOL）→ 保持 WPF 原版行为（映射到该行）
using System;
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
	public class FileDiffHoverOutsideTextAreaTests
	{
		private static Diff MakeDiff()
		{
			var lines = new string[]
			{
				"c1 ctx1\n", "c1 ctx2\n", "c1 ctx3\n", "c1 deleted\n", "c1 added\n",
				"c1 post1\n", "c1 post2\n", "c1 post3\n", "c1 post4\n", "c1 post5\n",
				"c2 ctx1\n", "c2 ctx2\n", "c2 ctx3\n", "c2 deleted\n", "c2 added\n",
				"c2 post1\n", "c2 post2\n", "c2 post3\n", "c2 post4\n", "c2 post5\n"
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

		private sealed class HoverResult
		{
			public CommitDiffSelectedRange Active;
			public bool AdornerShown;
		}

		private static void RunOnUi(Action<Window, SplitCommitTextDiffControl, CommitCodeEditor, Func<Point, HoverResult>> action)
		{
			HeadlessAppBootstrap.EnsureStarted();
			var task = Dispatcher.UIThread.InvokeAsync(delegate
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
				var adornerField = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_adorner",
					BindingFlags.NonPublic | BindingFlags.Instance);

				Func<Point, HoverResult> hover = p =>
				{
					HeadlessWindowExtensions.MouseMove(window, p, global::Avalonia.Input.RawInputModifiers.None);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					HeadlessWindowExtensions.CaptureRenderedFrame(window);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					return new HoverResult
					{
						Active = editor.ActiveChunk,
						AdornerShown = adornerField.GetValue(layer) != null
					};
				};

				try
				{
					action(window, control, editor, hover);
				}
				finally
				{
					window.Close();
				}
			});
			task.Wait(System.TimeSpan.FromMilliseconds(60000));
			Assert.Equal(DispatcherOperationStatus.Completed, task.Status);
			Dispatcher.UIThread.InvokeAsync(delegate { }, DispatcherPriority.Background).Wait(System.TimeSpan.FromMilliseconds(60000));
		}

		[Fact]
		public void Hover_LeftScrollbarStrip_AtHunkRow_DoesNotActivateChunkOrShowAdorner()
		{
			RunOnUi((window, control, editor, hover) =>
			{
				var tv = editor.TextArea.TextView;
				var tvOrigin = tv.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
				// 第5行 = c1 deleted（hunk 内），取行中心 Y
				var vl5 = tv.GetVisualLine(5);
				double yDel = vl5.VisualTop - tv.ScrollOffset.Y + vl5.Height / 2;
				// 文本区内基线：必须激活（证明守卫没有误伤正常 hover）
				var baseline = hover(new Point(tvOrigin.X + 100, tvOrigin.Y + yDel));
				Assert.NotNull(baseline.Active);
				Assert.True(baseline.AdornerShown);
				// 左侧滚动条/映射条（X < tvOrigin.X）：修复前 active=hunk + adorner，修复后必须全空
				foreach (double dx in new double[] { 2, 15, 30, 45 })
				{
					var r = hover(new Point(dx, tvOrigin.Y + yDel));
					Assert.Null(r.Active);
					Assert.False(r.AdornerShown);
				}
			});
		}

		[Fact]
		public void Hover_OutsideTextArea_OtherRegions_DoesNotActivateChunkOrShowAdorner()
		{
			RunOnUi((window, control, editor, hover) =>
			{
				var tv = editor.TextArea.TextView;
				var tvOrigin = tv.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
				double editorWidth = editor.Bounds.Width;
				var vl5 = tv.GetVisualLine(5);
				double yDel = vl5.VisualTop - tv.ScrollOffset.Y + vl5.Height / 2;
				var vl9 = tv.GetVisualLine(9); // c1 post3：hunk 外 context
				double yCtx = vl9.VisualTop - tv.ScrollOffset.Y + vl9.Height / 2;
				var lastLine = tv.GetVisualLine(editor.Document.LineCount - 1);
				double yBelowDoc = lastLine.VisualTop - tv.ScrollOffset.Y + lastLine.Height + 30;
				double rightEdge = editorWidth - 6;

				// 左条 + hunk 外 context 行
				var r = hover(new Point(20, tvOrigin.Y + yCtx));
				Assert.Null(r.Active);
				Assert.False(r.AdornerShown);
				// 右缘滚动条区 + context 行
				r = hover(new Point(rightEdge, tvOrigin.Y + yCtx));
				Assert.Null(r.Active);
				Assert.False(r.AdornerShown);
				// 文档末行以下的空白区（文本区中心/左条/右缘三个 X）
				if (yBelowDoc < editor.Bounds.Height - 4)
				{
					r = hover(new Point(tvOrigin.X + 100, yBelowDoc));
					Assert.Null(r.Active);
					Assert.False(r.AdornerShown);
					r = hover(new Point(20, yBelowDoc));
					Assert.Null(r.Active);
					Assert.False(r.AdornerShown);
					r = hover(new Point(rightEdge, yBelowDoc));
					Assert.Null(r.Active);
					Assert.False(r.AdornerShown);
				}
				// 顶缘（左条 X、Y=2）
				r = hover(new Point(20, 2));
				Assert.Null(r.Active);
				Assert.False(r.AdornerShown);
			});
		}

		[Fact]
		public void Hover_InsideTextView_RightOfTextEnd_MapsToLine_LikeWpfOriginal()
		{
			RunOnUi((window, control, editor, hover) =>
			{
				var tv = editor.TextArea.TextView;
				var tvOrigin = tv.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
				// hunk 行的行尾右侧（TextView 内 past-EOL）：WPF 原版映射到该行 → hunk 激活
				var vl5 = tv.GetVisualLine(5);
				double yDel = vl5.VisualTop - tv.ScrollOffset.Y + vl5.Height / 2;
				var r = hover(new Point(tvOrigin.X + tv.Bounds.Width - 20, tvOrigin.Y + yDel));
				Assert.NotNull(r.Active);
				Assert.True(r.AdornerShown);
				// 离开到 hunk 外 context 行 → 清空
				var vl9 = tv.GetVisualLine(9);
				double yCtx = vl9.VisualTop - tv.ScrollOffset.Y + vl9.Height / 2;
				r = hover(new Point(tvOrigin.X + tv.Bounds.Width - 20, tvOrigin.Y + yCtx));
				Assert.Null(r.Active);
				Assert.False(r.AdornerShown);
			});
		}
	}
}
