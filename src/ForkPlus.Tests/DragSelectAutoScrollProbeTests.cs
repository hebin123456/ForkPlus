// 回归（2026-09-17，"FileDiff 大区域从下往上拖选暂存内容，界面弹上去/选区冻结"）。
// 场景：变更块行数远超视口高度 → 滚到底部 → 从下往上拖选 → 指针越过视口上缘后
// 观察滚动偏移是否失控上跳。修复前两个缺陷：
//   1) caret 跟随滚动未被拖选抑制（_pointerSelecting 从未置位——SelectionMouseHandler
//      把 Pressed 事件 Handled=true，普通订阅收不到）→ 每次 move 触发 ScrollTo 的
//      "caret 行居中"跳变，越过视口上缘后一路弹跳到文档顶部；
//   2) 悬浮 Stage/Discard 按钮首次构建时 AdornerLayer 重建窗口内容树，摘除瞬间
//      指针捕获被挪到祖先元素，拖选管线断裂（选区冻结在两行、Release 收不到）。
// 用 headless 真实鼠标事件序列（press → 逐级上移 → 越过顶部后持续 wiggle）驱动
// CommitCodeEditor（DiffViewMode.Split，未暂存），第一轮拖选即触发悬浮按钮/重建。
// 断言：拖选期间无"居中跳变"级滚动（单步 ≤40px）、越过顶部后只按行步进、
// 最终不弹到文档顶部、选区按预期扩展、抑制标志抬起后复位。
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DragSelectAutoScrollProbeTests
	{
		private static Diff MakeBigDiff(int addedCount)
		{
			// 3 context + 3 deleted + N added + 3 context（真实 git diff 每行带尾部 \n）
			var lines = new System.Collections.Generic.List<string>();
			for (int i = 1; i <= 3; i++)
			{
				lines.Add("context line " + i + "\n");
			}
			int deletedStart = lines.Count;
			for (int i = 1; i <= 3; i++)
			{
				lines.Add("deleted line " + i + "\n");
			}
			int addedStart = lines.Count;
			for (int i = 1; i <= addedCount; i++)
			{
				lines.Add("added line " + i + "\n");
			}
			int postStart = lines.Count;
			for (int i = 1; i <= 3; i++)
			{
				lines.Add("tail context line " + i + "\n");
			}
			var subChunk = new SubChunk(
				new Range(0, deletedStart),
				new Range(deletedStart, addedStart),
				new Range(addedStart, postStart),
				new Range(postStart, lines.Count),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(10, 3, 20, addedCount, null, new[] { subChunk });
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines.ToArray(), new[] { chunk }, null, Diff.FileType.Text, false);
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
				return "TIMEOUT(marker/main-loop): 主循环陷入无限循环（posted job / layout）";
			}
			return task.Result;
		}

		// 单步滚动跳变的上限（px）：正常按行步进 ~15px（行高），居中跳变 ~190px（视口一半）
		private const double MaxStepPx = 40.0;

		[Fact]
		public void Probe_DragSelect_BottomToTop_BeyondViewport()
		{
			string report = RunWithTimeout(delegate
			{
				var sb = new System.Text.StringBuilder();
				// 每次事件后的状态快照（tag, offsetY, caret, selStart行, selEnd行, ptrSelecting）
				var snaps = new System.Collections.Generic.List<(string tag, double offset, int caret, int selStart, int selEnd, bool ptrSelecting)>();
				try
				{
					var diff = MakeBigDiff(150);
					// 顶部留 40px 空白行：给"指针越过编辑器上缘"留窗口内空间
					var grid = new Grid { RowDefinitions = new RowDefinitions("40,*") };
					var editor = new CommitCodeEditor(DiffViewMode.Split);
					editor.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
					editor.IsStaged = false;
					Grid.SetRow(editor, 1);
					grid.Children.Add(editor);
					var window = new Window { Width = 900, Height = 420, Content = grid };
					window.Show();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

					var textView = editor.TextArea.TextView;
					sb.AppendLine("doc lines=" + editor.Document.LineCount
						+ ", textView bounds=" + textView.Bounds
						+ ", docHeight=" + textView.DocumentHeight);

					// ===== 滚到底部 =====
					editor.SetScrollPosition(double.MaxValue);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					double offsetBottom = textView.ScrollOffset.Y;
					sb.AppendLine("after scroll-to-bottom: offset.Y=" + offsetBottom.ToString("F1"));

					// TextView 原点的窗口坐标
					var tvOriginOpt = textView.TranslatePoint(new Point(0, 0), window);
					if (!tvOriginOpt.HasValue)
					{
						return "FAIL: cannot translate TextView origin to window";
					}
					Point tvOrigin = tvOriginOpt.Value;
					sb.AppendLine("textView origin in window=" + tvOrigin.X.ToString("F1") + "," + tvOrigin.Y.ToString("F1"));

					double x = tvOrigin.X + 300;
					double pressY = tvOrigin.Y + textView.Bounds.Height - 20; // 视口底部附近按下

				global::Avalonia.Input.IPointer trackedPointer = null;
				// 诊断：TextArea 实际收到的事件（含已 handled 的），供失败时排查
				int taMoveCount = 0;
				editor.TextArea.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent,
					delegate (object s, global::Avalonia.Input.PointerPressedEventArgs e)
					{
						trackedPointer = e.Pointer;
					}, global::Avalonia.Interactivity.RoutingStrategies.Bubble | global::Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
				editor.TextArea.AddHandler(Avalonia.Input.InputElement.PointerMovedEvent,
					delegate (object s, global::Avalonia.Input.PointerEventArgs e)
					{
						taMoveCount++;
						var p = e.GetPosition(textView);
						sb.AppendLine("  [TA move#" + taMoveCount + "] pos=" + p.X.ToString("F0") + "," + p.Y.ToString("F0")
							+ " handled=" + e.Handled
							+ " leftBtn=" + e.GetCurrentPoint(editor.TextArea).Properties.IsLeftButtonPressed
							+ " captured=" + (e.Pointer.Captured?.GetType().Name ?? "<null>"));
					}, global::Avalonia.Interactivity.RoutingStrategies.Bubble | global::Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

				string Snap(string tag)
				{
					var sel = editor.TextArea.Selection;
					int sLine = sel.IsEmpty ? -1 : editor.Document.GetLineByOffset(sel.SurroundingSegment.Offset).LineNumber;
					int eLine = sel.IsEmpty ? -1 : editor.Document.GetLineByOffset(sel.SurroundingSegment.EndOffset).LineNumber;
					string captured = trackedPointer == null ? "n/a" : (trackedPointer.Captured?.GetType().FullName ?? "<null>");
					bool pointerSelecting = (bool)(typeof(CodeEditor).GetField("_pointerSelecting",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(editor) ?? false);
					snaps.Add((tag, textView.ScrollOffset.Y, editor.TextArea.Caret.Line, sLine, eLine, pointerSelecting));
					return tag + ": offset.Y=" + textView.ScrollOffset.Y.ToString("F1")
						+ " caret=" + editor.TextArea.Caret.Line
						+ " sel=" + (sel.IsEmpty ? "empty" : sLine + "-" + eLine)
						+ " ptrSelecting=" + pointerSelecting
						+ " ptrCaptured=" + captured;
				}

					// ===== 第一轮拖选（触发悬浮按钮/AdornerLayer 首次重建窗口内容树） =====
					HeadlessWindowExtensions.MouseDown(window, new Point(x, pressY), Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					sb.AppendLine(Snap("press@" + pressY.ToString("F0")));

				// 逐级上移（每步 15px），跨过视口上缘后继续移到编辑器上方（窗口内 40px 空白区）
				// 修正：真实拖拽期间 move 事件携带"左键按下"修饰键（headless 不会自动带，须显式传）
				double y = pressY;
					while (y > tvOrigin.Y - 15)
					{
						y -= 15;
						HeadlessWindowExtensions.MouseMove(window, new Point(x, y), Avalonia.Input.RawInputModifiers.LeftMouseButton);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine(Snap("move@" + y.ToString("F0")));
					}

				// ===== 越过顶部后"悬停不动 + 微 wiggle"（模拟按住不放） =====
				double aboveY = tvOrigin.Y - 15;
					for (int w = 0; w < 5; w++)
					{
						HeadlessWindowExtensions.MouseMove(window, new Point(x, aboveY + (w % 2 == 0 ? 2 : -2)), Avalonia.Input.RawInputModifiers.LeftMouseButton);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine(Snap("wiggle" + w));
					}

					HeadlessWindowExtensions.MouseUp(window, new Point(x, aboveY), Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					sb.AppendLine(Snap("release"));
					sb.AppendLine("=== drag1 end（触发悬浮按钮/AdornerLayer 首次重建窗口内容树）===");

					// ===== 第二次拖选（AdornerLayer 已就位，捕获应保持——复现真实用户日常场景） =====
					// 注意：drag1 完成后选区覆盖底部按下点；直接在旧选区内按下并拖动会走
					// AvaloniaEdit 的文本拖放（PossibleDragStart → StartDrag）而非拖选，那是
					// 编辑器标准行为、与本 bug 无关——先清空选区模拟"在旧选区外按下"。
					editor.TextArea.ClearSelection();
					editor.SetScrollPosition(double.MaxValue);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					sb.AppendLine(Snap("scroll2bottom"));
					taMoveCount = 0;

					double pressY2 = tvOrigin.Y + textView.Bounds.Height - 20;
					HeadlessWindowExtensions.MouseDown(window, new Point(x, pressY2), Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					sb.AppendLine(Snap("press2@" + pressY2.ToString("F0")));

					double y2 = pressY2;
					while (y2 > tvOrigin.Y - 15)
					{
						y2 -= 15;
						HeadlessWindowExtensions.MouseMove(window, new Point(x, y2), Avalonia.Input.RawInputModifiers.LeftMouseButton);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine(Snap("move2@" + y2.ToString("F0")));
					}

					// 越过顶部后持续 wiggle（按住不放，指针停在视口上方）
					double aboveY2 = tvOrigin.Y - 15;
					for (int w = 0; w < 6; w++)
					{
						HeadlessWindowExtensions.MouseMove(window, new Point(x, aboveY2 + (w % 2 == 0 ? 2 : -2)), Avalonia.Input.RawInputModifiers.LeftMouseButton);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine(Snap("wiggle2_" + w));
					}

					HeadlessWindowExtensions.MouseUp(window, new Point(x, aboveY2), Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					sb.AppendLine(Snap("release2"));

					// ===== 期望值参考：起始偏移 vs 结束偏移（跳变幅度） =====
					sb.AppendLine("drag2 deltaOffset=" + (textView.ScrollOffset.Y - offsetBottom).ToString("F1")
						+ " (bottom=" + offsetBottom.ToString("F1") + " → end=" + textView.ScrollOffset.Y.ToString("F1") + ")");

					// ===== 断言 =====
					// 找到两轮拖选各自的 [press..release) 区间
					int r1 = snaps.FindIndex(s => s.tag.StartsWith("press@"));
					int r1End = snaps.FindIndex(s => s.tag == "release");
					int r2 = snaps.FindIndex(s => s.tag.StartsWith("press2@"));
					int r2End = snaps.FindIndex(s => s.tag == "release2");
					if (r1 < 0 || r1End < 0 || r2 < 0 || r2End < 0)
					{
						return "FAIL: missing drag phase markers; report:\n" + sb;
					}

					// 1) 拖选期间单步滚动 ≤ MaxStepPx（视口 380px 的"居中跳变"约 ±190px）
					for (int i = r1; i <= r1End; i++)
					{
						if (i > r1 && Math.Abs(snaps[i].offset - snaps[i - 1].offset) > MaxStepPx)
						{
							return "FAIL(drag1 step-jump): " + snaps[i - 1].tag + "→" + snaps[i].tag
								+ " delta=" + (snaps[i].offset - snaps[i - 1].offset).ToString("F1") + "px; report:\n" + sb;
						}
					}
					for (int i = r2; i <= r2End; i++)
					{
						if (i > r2 && Math.Abs(snaps[i].offset - snaps[i - 1].offset) > MaxStepPx)
						{
							return "FAIL(drag2 step-jump): " + snaps[i - 1].tag + "→" + snaps[i].tag
								+ " delta=" + (snaps[i].offset - snaps[i - 1].offset).ToString("F1") + "px; report:\n" + sb;
						}
					}

					// 2) 越过顶部后不弹到文档顶部（最终偏移仍在文档底部区域：≥ 底部 - 2×视口高）
					double floor = offsetBottom - 2 * textView.Bounds.Height;
					if (snaps[r1End].offset < floor || snaps[r2End].offset < floor)
					{
						return "FAIL(bounce-to-top): drag1 end=" + snaps[r1End].offset.ToString("F1")
							+ " drag2 end=" + snaps[r2End].offset.ToString("F1") + " floor=" + floor.ToString("F1") + "; report:\n" + sb;
					}

					// 3) 选区按预期扩展：从底部行（≈160）向上扩到 ≥30 行（越过视口上缘后仍继续）
					int drag1End = snaps[r1End].selEnd;
					int drag1Start = snaps[r1End].selStart;
					if (drag1Start < 0 || drag1Start > 135 || drag1End != 160)
					{
						return "FAIL(drag1 selection): sel=" + drag1Start + "-" + drag1End + "（期望 anchor=160、上端扩到 ≤135）; report:\n" + sb;
					}
					if (snaps[r2End].selStart < 0 || snaps[r2End].selStart > 135 || snaps[r2End].selEnd != 160)
					{
						return "FAIL(drag2 selection): sel=" + snaps[r2End].selStart + "-" + snaps[r2End].selEnd + "; report:\n" + sb;
					}

					// 4) 抑制标志抬起后复位（悬挂会导致后续键盘/程序化 caret 移动不跟随滚动）
					if (snaps[r1End].ptrSelecting || snaps[r2End].ptrSelecting)
					{
						return "FAIL(ptrSelecting stuck): drag1=" + snaps[r1End].ptrSelecting + " drag2=" + snaps[r2End].ptrSelecting + "; report:\n" + sb;
					}

					window.Close();
					return "PASS; report:\n" + sb;
				}
				catch (Exception e)
				{
					return "EXCEPTION: " + e;
				}
			}, 30000);
			System.IO.File.WriteAllText("/tmp/drag_select_autoscroll_probe.txt", report);
			Assert.True(report.StartsWith("PASS"), report);
		}
	}
}
