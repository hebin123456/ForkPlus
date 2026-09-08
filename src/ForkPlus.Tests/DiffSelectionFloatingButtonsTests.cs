// 回归（问题6，2026-09-04）："选中一小块变更区域应该出来悬浮的变更和丢弃，右键菜单是另外的行为"。
// 原版 WPF 行为：选中 diff 中若干行（或 hover 一个 hunk）→ 选区右上角出现悬浮
// Stage/Discard...（已暂存时 Unstage）按钮（ChunkSelectionLayer.ButtonsAdorner）；
// 右键菜单只有 OpenFileInExternalEditor/HunkHistory/Copy/CopyAsPatch，不含
// Stage/Discard（此前迁移版误把入口塞进右键菜单，本回归验证已移除）。
// 覆盖：
//   Phase1 split 未暂存：程序化选区 → 悬浮 [Stage, Discard...] 出现且定位到选区顶部，
//          点击 Stage → CommitCodeEditor.Stage 以该 editor 触发；
//   Phase2 split 已暂存：选区 → 悬浮 [Unstage]；
//   Phase3 side-by-side：NEW 侧选区 → 该侧出按钮，OLD 侧无；
//   Phase4 真实鼠标：hover hunk 出按钮 → 拖选保持 → 点击别处取消选区 →
//          hover hunk 再移开 → 按钮移除（ActiveChunk 转移路径）；
//   Phase5 右键菜单回归护栏：AddSelectionPatchMenuItems 不复存在。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiffSelectionFloatingButtonsTests
	{
		private static Diff MakeDiff()
		{
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
			var subChunk = new SubChunk(
				new Range(0, 3),
				new Range(3, 5),
				new Range(5, 8),
				new Range(8, 11),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(10, 6, 20, 7, null, new[] { subChunk });
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines, new[] { chunk }, null, Diff.FileType.Text, false);
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

		// ===== 反射助手：拿私有层与悬浮按钮 =====
		private static object GetLayer(CommitCodeEditor editor)
		{
			FieldInfo f = typeof(CommitCodeEditor).GetField("_diffSelectionLayer",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(f);
			return f.GetValue(editor);
		}

		private static object GetAdorner(object layer)
		{
			FieldInfo f = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_adorner",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(f);
			return f.GetValue(layer);
		}

		private static List<FloatingButton> CollectFloatingButtons(object adorner)
		{
			var result = new List<FloatingButton>();
			if (adorner == null)
			{
				return result;
			}
			PropertyInfo childProp = adorner.GetType().GetProperty("Child");
			Control child = childProp?.GetValue(adorner) as Control;
			if (child == null)
			{
				return result;
			}
			Collect(child, result);
			return result;
		}

		private static void Collect(Visual v, List<FloatingButton> result)
		{
			if (v is FloatingButton fb)
			{
				result.Add(fb);
			}
			foreach (Visual c in v.GetVisualChildren())
			{
				Collect(c, result);
			}
		}

		private static FloatingButton FindButton(List<FloatingButton> buttons, string content)
		{
			return buttons.FirstOrDefault(b => (b.Content as string) == content);
		}

		[Fact]
		public void Probe_SelectionFloatingButtons()
		{
			var sbHolder = new System.Text.StringBuilder[1];
			string report;
			try
			{
				report = RunWithTimeout(delegate
				{
					var sb = new System.Text.StringBuilder();
					sbHolder[0] = sb;
					try
					{
						var diff = MakeDiff();
						string expectStage = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Stage");
						string expectDiscard = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Discard...");
						string expectUnstage = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Unstage");

						// ===== Phase1：split 未暂存，程序化选区 → 悬浮 Stage/Discard =====
						var window = new Window { Width = 900, Height = 400 };
						window.Show();
						Dispatcher.UIThread.RunJobs();

						var editor1 = new CommitCodeEditor(DiffViewMode.Split);
						editor1.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
						editor1.IsStaged = false;
						window.Content = editor1;
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var layer1 = GetLayer(editor1);
						Assert.Null(GetAdorner(layer1)); // 未选中时无悬浮按钮

						int selStart = editor1.Text.IndexOf("deleted line 1", StringComparison.Ordinal);
						Assert.True(selStart >= 0, "Phase1: 找不到 deleted line 1");
						editor1.Select(selStart, 25);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						// 强制真实渲染一帧：Render → DrawSelectionBorder → ShowChunkAdorner（选区顶部）
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var adorner1 = GetAdorner(layer1);
						sb.AppendLine("phase1 adorner=" + (adorner1 != null));
						Assert.NotNull(adorner1);

						var buttons1 = CollectFloatingButtons(adorner1);
						sb.AppendLine("phase1 buttons=" + string.Join("|", buttons1.Select(b => b.Content)));
						Assert.Equal(2, buttons1.Count);
						Assert.NotNull(FindButton(buttons1, expectStage));
						Assert.NotNull(FindButton(buttons1, expectDiscard));

						// 定位：Tag 偏移 Y ≈ 选区首行 VisualTop + 15（ShowChunkAdorner 内 num=15）。
						// 用选区首行（而非硬编码行号）取预期——文档首部可能含 hunk 头等额外行。
						int firstSelLine = editor1.Document.GetLineByOffset(editor1.SelectionStart).LineNumber;
						var vlSel = editor1.TextArea.TextView.GetVisualLine(firstSelLine);
						Assert.NotNull(vlSel);
						var firstLine = editor1.Document.GetLineByNumber(firstSelLine);
						sb.AppendLine("phase1 firstSelLine=" + firstSelLine
							+ ", lineText=" + editor1.Document.GetText(firstLine.Offset, firstLine.Length).TrimEnd());
						double expectedY = vlSel.VisualTop - editor1.TextArea.TextView.ScrollOffset.Y + 15.0;
						var tag1 = ((Control)adorner1).Tag;
						sb.AppendLine("phase1 tag=" + tag1 + ", expectedY=" + expectedY);
						Assert.True(tag1 is Point p1 && Math.Abs(p1.Y - expectedY) <= 3.0,
							"phase1: 悬浮按钮应定位到选区顶部（tag=" + tag1 + ", expectedY=" + expectedY + "）");

						// 点击悬浮 Stage → editor1.Stage 事件触发（参数为该 editor）
						CommitCodeEditor stageArg = null;
						editor1.Stage += delegate (object s, CommitCodeEditor ed) { stageArg = ed; };
						FindButton(buttons1, expectStage).RaiseEvent(
							new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						Assert.Same(editor1, stageArg);
						sb.AppendLine("phase1 Stage click → event OK");

						// patch 提取链路（悬浮按钮 Stage 与右键原路径同源）
						int[] patchLines = editor1.GetSelectedPatchLines();
						sb.AppendLine("phase1 patchLines=" + (patchLines == null ? "null" : string.Join(",", patchLines)));
						Assert.NotNull(patchLines);
						Assert.True(patchLines.Length > 0, "phase1: 选区应映射到非空 patch lines");

						// ===== Phase2：split 已暂存 → 悬浮 [Unstage] =====
						var editor2 = new CommitCodeEditor(DiffViewMode.Split);
						editor2.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Staged);
						editor2.IsStaged = true;
						window.Content = editor2;
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var layer2 = GetLayer(editor2);
						int selStart2 = editor2.Text.IndexOf("deleted line 1", StringComparison.Ordinal);
						editor2.Select(selStart2, 25);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var adorner2 = GetAdorner(layer2);
						sb.AppendLine("phase2 adorner=" + (adorner2 != null));
						Assert.NotNull(adorner2);
						var buttons2 = CollectFloatingButtons(adorner2);
						sb.AppendLine("phase2 buttons=" + string.Join("|", buttons2.Select(b => b.Content)));
						Assert.Single(buttons2);
						Assert.NotNull(FindButton(buttons2, expectUnstage));

						CommitCodeEditor unstageArg = null;
						editor2.UnStage += delegate (object s, CommitCodeEditor ed) { unstageArg = ed; };
						FindButton(buttons2, expectUnstage).RaiseEvent(
							new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						Assert.Same(editor2, unstageArg);
						sb.AppendLine("phase2 Unstage click → event OK");

						// ===== Phase3：side-by-side，NEW 侧选区 → 该侧出按钮 =====
						var editorOld = new CommitCodeEditor(DiffViewMode.SideBySideOld);
						var editorNew = new CommitCodeEditor(DiffViewMode.SideBySideNew);
						editorNew.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
						editorNew.IsStaged = false;
						editorOld.Sync(editorNew);
						window.Content = new Grid
						{
							ColumnDefinitions = new ColumnDefinitions("450,450"),
							Children = { editorOld, editorNew }
						};
						Grid.SetColumn(editorOld, 0);
						Grid.SetColumn(editorNew, 1);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var layerOld = GetLayer(editorOld);
						var layerNew = GetLayer(editorNew);
						int selStart3 = editorNew.Text.IndexOf("added line 1", StringComparison.Ordinal);
						editorNew.Select(selStart3, 20);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var adornerNew = GetAdorner(layerNew);
						sb.AppendLine("phase3 adornerNew=" + (adornerNew != null)
							+ ", adornerOld=" + (GetAdorner(layerOld) != null));
						Assert.NotNull(adornerNew);
						Assert.Null(GetAdorner(layerOld)); // 选区在 NEW 侧，OLD 侧不应出按钮
						var buttons3 = CollectFloatingButtons(adornerNew);
						sb.AppendLine("phase3 buttons=" + string.Join("|", buttons3.Select(b => b.Content)));
						Assert.Equal(2, buttons3.Count);
						Assert.NotNull(FindButton(buttons3, expectStage));
						Assert.NotNull(FindButton(buttons3, expectDiscard));

						// ===== Phase4：真实鼠标（hover → 拖选 → 取消 → hover → 移开 → 移除）=====
						var editor4 = new CommitCodeEditor(DiffViewMode.Split);
						editor4.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
						editor4.IsStaged = false;
						window.Content = editor4;
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var layer4 = GetLayer(editor4);

						var vl6 = editor4.TextArea.TextView.GetVisualLine(6); // added line 1
						Assert.NotNull(vl6);
						double yHunk = vl6.VisualTop - editor4.TextArea.TextView.ScrollOffset.Y + 6;
						double yCtx = editor4.TextArea.TextView.GetVisualLine(1).VisualTop
							- editor4.TextArea.TextView.ScrollOffset.Y + 6;

						// 4a. hover 到 hunk 上 → 悬浮按钮出现（hover 模式）
						HeadlessWindowExtensions.MouseMove(window, new Point(250, yHunk), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine("phase4a hover adorner=" + (GetAdorner(layer4) != null));
						Assert.NotNull(GetAdorner(layer4));

						// 4b. 拖选 added line 1~3 → 选区存在，按钮保持
						HeadlessWindowExtensions.MouseDown(window, new Point(250, yHunk), global::Avalonia.Input.MouseButton.Left, global::Avalonia.Input.RawInputModifiers.None);
						for (double y = yHunk; y <= yHunk + 40; y += 4)
						{
							HeadlessWindowExtensions.MouseMove(window, new Point(250, y), global::Avalonia.Input.RawInputModifiers.None);
						}
						HeadlessWindowExtensions.MouseUp(window, new Point(250, yHunk + 40), global::Avalonia.Input.MouseButton.Left, global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine("phase4b drag selLength=" + editor4.SelectionLength
							+ ", adorner=" + (GetAdorner(layer4) != null));
						Assert.True(editor4.SelectionLength > 0, "phase4b: 拖选应产生选区");
						Assert.NotNull(GetAdorner(layer4));

						// 4c. 点击 context line 1 取消选区（ActiveChunk 已为 null，按钮暂留——与原版一致）
						HeadlessWindowExtensions.MouseMove(window, new Point(250, yCtx), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.MouseDown(window, new Point(250, yCtx), global::Avalonia.Input.MouseButton.Left, global::Avalonia.Input.RawInputModifiers.None);
						HeadlessWindowExtensions.MouseUp(window, new Point(250, yCtx), global::Avalonia.Input.MouseButton.Left, global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine("phase4c deselect selLength=" + editor4.SelectionLength
							+ ", adorner=" + (GetAdorner(layer4) != null));
						Assert.Equal(0, editor4.SelectionLength);

						// 4d. hover hunk（chunk 转移）→ 移回 context（chunk→null + 无选区）→ 按钮移除
						HeadlessWindowExtensions.MouseMove(window, new Point(250, yHunk), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.MouseMove(window, new Point(250, yCtx), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						sb.AppendLine("phase4d leave-hunk adorner=" + (GetAdorner(layer4) != null));
						Assert.Null(GetAdorner(layer4));

						window.Close();
					}
					catch (Exception e)
					{
						sb.AppendLine("EXCEPTION: " + e);
						throw;
					}
					return sb.ToString();
				}, 30000);
			}
			catch (Exception ex)
			{
				report = (sbHolder[0]?.ToString() ?? "") + "\nOUTER EXCEPTION: " + ex;
			}
			System.IO.File.WriteAllText("/tmp/diff_floating_buttons_probe.txt", report);
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
			Assert.DoesNotContain("OUTER EXCEPTION", report);
		}

		// 右键菜单回归护栏：Stage/Discard 入口不得回到右键菜单（原版只有悬浮按钮 + 键盘快捷键）。
		[Fact]
		public void ContextMenu_DoesNotContainSelectionPatchItems()
		{
			MethodInfo mi = typeof(CommitFileDiffControl).GetMethod("AddSelectionPatchMenuItems",
				BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			Assert.Null(mi);
		}
	}
}
