// 回归（2026-10-18，"FileDiff 视图 暂存浮窗一直飘来飘去"）。
// FileDiff 视图（CommitFileDiffControl → CommitTextDiffControl → CommitCodeEditor →
// ChunkSelectionLayer 系）的暂存/丢弃浮窗此前存在两类漂移：
//   A. 闪跳到编辑器顶部再跳回：InvalidateAdornerVisibility 保留分支调 ShowChunkAdorner(0.0)，
//      每次跨块 hover（ActiveChunk 转移）都把浮窗先写到编辑器顶部（Y≈15），下一帧 Render
//      才回到块顶部——无头环境里闪写与纠正会在同一批 job 里完成，故用 Tag 写入监听器
//      捕获"伪造位置写入"，而不是断言最终位置（新旧代码最终位置一致，无法判别）。
//   B. 滚轮经浮窗转发滚动时 remove/recreate 循环：指针在浮窗上 → 编辑器 MouseLeave 清空
//      _lastPointerPosition → ScrollOffsetChanged → RefreshActiveChunk 取不到块 →
//      ActiveChunk=null → RemoveChunkAdorner 把浮窗从指针下删掉 → 编辑器 PointerEntered
//      又立刻重建 → 每格滚一动一次，浮窗反复消失重现并跳位。
// 覆盖：
//   DriftA_HoverBetweenChunksNeverFlashToTop：双 hunk diff，块A→块B 转移期间监听浮窗
//          Tag 写入，断言每次写入都是两个块的真实锚点（GetRectForChunk.Top+15）；
//   DriftA_SideBySideNeverFlashToTop：side-by-side 双 hunk，NEW 侧同样断言；
//   DriftB_StagedWheelOverButtonsKeepsButtonsAndFollows：已暂存视图，复现生产态
//          （指针在浮窗上 → _isPointerOverAdorner=true 且 _lastPointerPosition=null），
//          滚动后浮窗不消失且跟随块移动。
// 预期位置统一取生产锚点：ChunkSelectionLayer.GetRectForChunk(ActiveChunk).Top + 15，
// 与 DrawChunk → ShowAdornerOnMouseOver 的定位完全同源，避免测试自行推算行号。
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
using ForkPlus.Git.Diff.Parsing;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class FileDiffFloatingWindowDriftTests
	{
		// 双 hunk 补丁（git 原生格式，经生产 PatchParser 解析，保证 Chunk/SubChunk 结构真实）：
		// hunk1 块 + hunk2 块，中间隔 4 行上下文，供"跨块 hover 转移"测试。
		private const string TwoHunkPatch = @"diff --git a/sample.txt b/sample.txt
index 1111111..2222222 100644
--- a/sample.txt
+++ b/sample.txt
@@ -1,6 +1,7 @@
 context line 1
 context line 2
-deleted line 1
-deleted line 2
+added line 1
+added line 2
+added line 3
 context line 3
 context line 4
@@ -7,8 +8,9 @@
 gap line 1
 gap line 2
 gap line 3
 gap line 4
-deleted line 3
-deleted line 4
+added line 4
+added line 5
+added line 6
 gap line 5
 gap line 6
";

		private static Diff MakeTwoChunkDiff()
		{
			var parse = new PatchParser().Parse(TwoHunkPatch);
			Assert.True(parse.Succeeded, "补丁应解析成功");
			Diff diff = parse.Result.Diffs.FirstOrDefault();
			Assert.True(diff != null, "补丁应产出 Diff");
			Assert.True(diff.Chunks.Length >= 2, "补丁应含两个 hunk 块，实际=" + diff.Chunks.Length);
			return diff;
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

		// ===== 反射助手 =====
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

		private static double GetAdornerTagY(object adorner)
		{
			var tag = ((Control)adorner).Tag;
			Assert.True(tag is Point p, "Tag 应为 Point 偏移，实际=" + tag);
			return ((Point)tag).Y;
		}

		// 生产锚点：DrawChunk → ShowAdornerOnMouseOver(rect.Top) → ShowChunkAdornerCore
		// 的 top = GetRectForChunk(ActiveChunk).Top + 15。测试预期与生产同源。
		private static double GetProductionExpectedY(object layer)
		{
			FieldInfo fChunk = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_activeChunk",
				BindingFlags.NonPublic | BindingFlags.Instance);
			MethodInfo mRect = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetMethod("GetRectForChunk",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(fChunk);
			Assert.NotNull(mRect);
			var chunk = fChunk.GetValue(layer);
			Assert.True(chunk != null, "ActiveChunk 应已锚定");
			var rect = (Rect?)mRect.Invoke(layer, new[] { chunk });
			Assert.True(rect.HasValue, "GetRectForChunk 应返回块矩形");
			return rect.Value.Top + 15.0;
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

		private static bool GetPointerOverAdorner(object layer)
		{
			FieldInfo f = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_isPointerOverAdorner",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(f);
			return (bool)f.GetValue(layer);
		}

		private static void SetPointerOverAdorner(object layer, bool value)
		{
			FieldInfo f = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_isPointerOverAdorner",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(f);
			f.SetValue(layer, value);
		}

		private static void SetLastPointerPositionNull(object layer)
		{
			FieldInfo f = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_lastPointerPosition",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(f);
			f.SetValue(layer, null);
		}

		// 监听浮窗 Tag 写入：跨块 hover 转移期间，每次定位写入都应落在真实块锚点上。
		private static List<double> AttachTagRecorder(object adorner)
		{
			var recorded = new List<double>();
			((Control)adorner).PropertyChanged += delegate (object s, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
			{
				if (e.Property == Control.TagProperty && e.NewValue is Point p)
				{
					lock (recorded)
					{
						recorded.Add(p.Y);
					}
				}
			};
			return recorded;
		}

		// ===== A. 块A→块B hover 转移：Tag 写入只允许落在两个块的真实锚点上 =====
		[Fact]
		public void DriftA_HoverBetweenChunksNeverFlashToTop()
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
						var diff = MakeTwoChunkDiff();
						var window = new Window { Width = 900, Height = 400 };
						window.Show();
						Dispatcher.UIThread.RunJobs();

						var editor = new CommitCodeEditor(DiffViewMode.Split);
						editor.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
						editor.IsStaged = false;
						window.Content = editor;
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var layer = GetLayer(editor);
						var textView = editor.TextArea.TextView;

						// 块A/B 的 hover 行（deleted line 1 / deleted line 3）
						double yA = YOf(textView, editor, "deleted line 1");
						double yB = YOf(textView, editor, "deleted line 3");
						double yGap = YOf(textView, editor, "gap line 2");
						sb.AppendLine("yA=" + yA + ", yB=" + yB + ", yGap=" + yGap);

						// 1. hover 块A → 浮窗出现且定位到块A 锚点
						HeadlessWindowExtensions.MouseMove(window, new Point(250, yA), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var adorner = GetAdorner(layer);
						Assert.NotNull(adorner);
						double posA = GetProductionExpectedY(layer);
						Assert.True(Math.Abs(GetAdornerTagY(adorner) - posA) <= 3.0,
							"浮窗应定位到块A 顶部（tagY=" + GetAdornerTagY(adorner) + ", expected=" + posA + "）");

						// 2. 挂 Tag 写入监听 → hover 块B（ActiveChunk A→B 直接转移，浮窗不重建）
						var recorded = AttachTagRecorder(adorner);
						HeadlessWindowExtensions.MouseMove(window, new Point(250, yB), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						double posB = GetProductionExpectedY(layer);
						sb.AppendLine("posA=" + posA + ", posB=" + posB
							+ ", recorded=[" + string.Join(",", recorded) + "]");

						// 旧代码：InvalidateAdornerVisibility → ShowChunkAdorner(0.0) 会先把 Tag
						// 写到编辑器顶部（≈15），随后渲染帧才纠正——该"伪造位置写入"即闪跳。
						Assert.True(recorded.Count > 0, "跨块 hover 应发生 Tag 定位写入");
						foreach (double y in recorded)
						{
							Assert.True(Math.Abs(y - posA) <= 3.0 || Math.Abs(y - posB) <= 3.0,
								"跨块 hover 期间浮窗被写到伪造位置 y=" + y + "（块A=" + posA + ", 块B=" + posB
								+ "）——旧代码 ShowChunkAdorner(0.0) 闪顶");
						}
						Assert.Contains(recorded, y => Math.Abs(y - posB) <= 3.0);

						// 3. 最终定位到块B
						adorner = GetAdorner(layer);
						Assert.NotNull(adorner);
						Assert.True(Math.Abs(GetAdornerTagY(adorner) - posB) <= 3.0,
							"浮窗最终应定位到块B 顶部（tagY=" + GetAdornerTagY(adorner) + ", expected=" + posB + "）");

						// 4. 移出块（hunk 间 gap 行）→ 浮窗移除（原行为不变）
						HeadlessWindowExtensions.MouseMove(window, new Point(250, yGap), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						Assert.Null(GetAdorner(layer));

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
			System.IO.File.WriteAllText("/tmp/fildediff_drift_a_hover.txt", report);
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
			Assert.DoesNotContain("OUTER EXCEPTION", report);
		}

		// ===== A2. side-by-side：NEW 侧跨块 hover 转移，同样不闪顶 =====
		[Fact]
		public void DriftA_SideBySideNeverFlashToTop()
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
						var diff = MakeTwoChunkDiff();
						var window = new Window { Width = 900, Height = 400 };
						window.Show();
						Dispatcher.UIThread.RunJobs();

						// 与 SideBySideCommitTextDiffControl 相同的双侧 patch 装配方式
						var editorOld = new CommitCodeEditor(DiffViewMode.SideBySideOld);
						var editorNew = new CommitCodeEditor(DiffViewMode.SideBySideNew);
						VisualPatch.CreateSideBySideVisualPatch(diff, false, DiffLocation.Unstaged, out var vpOld, out var vpNew);
						editorOld.VisualPatch = vpOld;
						editorNew.VisualPatch = vpNew;
						editorOld.IsStaged = false;
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
						var layerNew = GetLayer(editorNew);
						var textViewNew = editorNew.TextArea.TextView;

						// NEW 侧块A/B 的 hover 行（added line 1 / added line 4）
						double yA = YOf(textViewNew, editorNew, "added line 1");
						double yB = YOf(textViewNew, editorNew, "added line 4");
						sb.AppendLine("yA=" + yA + ", yB=" + yB);

						// 1. hover NEW 侧块A → 浮窗出现且定位到块A 锚点
						HeadlessWindowExtensions.MouseMove(window, new Point(675, yA), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var adorner = GetAdorner(layerNew);
						Assert.NotNull(adorner);
						double posA = GetProductionExpectedY(layerNew);
						Assert.True(Math.Abs(GetAdornerTagY(adorner) - posA) <= 3.0,
							"NEW 侧浮窗应定位到块A 顶部（tagY=" + GetAdornerTagY(adorner) + ", expected=" + posA + "）");
						Assert.Equal(2, CollectFloatingButtons(adorner).Count); // 未暂存 → Stage + Discard...

						// 2. 挂 Tag 写入监听 → hover NEW 侧块B
						var recorded = AttachTagRecorder(adorner);
						HeadlessWindowExtensions.MouseMove(window, new Point(675, yB), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						double posB = GetProductionExpectedY(layerNew);
						sb.AppendLine("posA=" + posA + ", posB=" + posB
							+ ", recorded=[" + string.Join(",", recorded) + "]");

						Assert.True(recorded.Count > 0, "跨块 hover 应发生 Tag 定位写入");
						foreach (double y in recorded)
						{
							Assert.True(Math.Abs(y - posA) <= 3.0 || Math.Abs(y - posB) <= 3.0,
								"side-by-side 跨块 hover 期间浮窗被写到伪造位置 y=" + y + "（块A=" + posA + ", 块B=" + posB
								+ "）——旧代码 ShowChunkAdorner(0.0) 闪顶");
						}
						Assert.Contains(recorded, y => Math.Abs(y - posB) <= 3.0);

						// 3. 最终定位到块B
						adorner = GetAdorner(layerNew);
						Assert.NotNull(adorner);
						Assert.True(Math.Abs(GetAdornerTagY(adorner) - posB) <= 3.0,
							"NEW 侧浮窗最终应定位到块B 顶部（tagY=" + GetAdornerTagY(adorner) + ", expected=" + posB + "）");

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
			System.IO.File.WriteAllText("/tmp/fildediff_drift_a2_sibling.txt", report);
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
			Assert.DoesNotContain("OUTER EXCEPTION", report);
		}

		// ===== B. 已暂存视图：指针在浮窗上滚动 → 浮窗不消失且跟随块移动 =====
		[Fact]
		public void DriftB_StagedWheelOverButtonsKeepsButtonsAndFollows()
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
						var diff = MakeTwoChunkDiff();
						string expectUnstage = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Unstage");

						// 视口压到 130px：文档溢出视口，可滚动（滚轮经浮窗转发滚动的生产前提）
						var window = new Window { Width = 900, Height = 130 };
						window.Show();
						Dispatcher.UIThread.RunJobs();

						var editor = new CommitCodeEditor(DiffViewMode.Split);
						editor.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Staged);
						editor.IsStaged = true;
						window.Content = editor;
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var layer = GetLayer(editor);
						var textView = editor.TextArea.TextView;

						// hover 块A 内行（deleted line 1）
						double yHunk = YOf(textView, editor, "deleted line 1");
						sb.AppendLine("yHunk=" + yHunk + " (window 130 内可见)");
						Assert.True(yHunk > 0 && yHunk < 125, "测试前提：hunk 行应在 130px 视口内（y=" + yHunk + "）");

						HeadlessWindowExtensions.MouseMove(window, new Point(250, yHunk), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						var adorner = GetAdorner(layer);
						Assert.NotNull(adorner);
						var buttons = CollectFloatingButtons(adorner);
						sb.AppendLine("buttons=" + string.Join("|", buttons.Select(b => b.Content)));
						Assert.Single(buttons);
						Assert.Equal(expectUnstage, buttons[0].Content); // 暂存对比视图 → [Unstage] 浮窗

						// 指针移到浮窗上 → 复现生产态：
						//   编辑器 MouseLeave 已把 _lastPointerPosition 清空、Adorner_PointerEntered
						//   已把 _isPointerOverAdorner 置位（无头环境可能不派发这两类事件，显式对齐）
						var ad = (Control)adorner;
						double bx = ad.GetValue(Canvas.LeftProperty);
						double by = ad.GetValue(Canvas.TopProperty);
						sb.AppendLine("buttons window pos=" + bx + "," + by);
						Assert.True(by > 0 && by < 120, "浮窗应在视口内（top=" + by + "）");
						HeadlessWindowExtensions.MouseMove(window, new Point(bx + 25, by + 10), global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						SetPointerOverAdorner(layer, true);
						SetLastPointerPositionNull(layer);

						// 滚动（与滚轮转发等价：ScrollOffsetChanged → RefreshActiveChunk）
						double scrollBefore = textView.ScrollOffset.Y;
						editor.ScrollToVerticalOffsetCompat(scrollBefore + 25);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						SetPointerOverAdorner(layer, true); // 防无头环境 pointer-over 重估触发延迟隐藏
						double scrollAfter = textView.ScrollOffset.Y;
						sb.AppendLine("scroll " + scrollBefore + " -> " + scrollAfter);
						Assert.True(scrollAfter > scrollBefore, "测试前提：滚动应生效");

						HeadlessWindowExtensions.CaptureRenderedFrame(window);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

						// 旧代码：ActiveChunk 被置空 → RemoveChunkAdorner → 浮窗在指针下消失后重建
						var adornerAfter = GetAdorner(layer);
						sb.AppendLine("after-scroll adorner=" + (adornerAfter != null)
							+ ", tagY=" + (adornerAfter == null ? -1 : GetAdornerTagY(adornerAfter)));
						Assert.True(adornerAfter != null, "指针在浮窗上滚动后，浮窗不应被移除");

						// 浮窗应跟随块移动到新视口位置（生产锚点：ActiveChunk 保持锚定 → rect.Top 已随滚动更新）
						double expectedY = GetProductionExpectedY(layer);
						Assert.True(Math.Abs(GetAdornerTagY(adornerAfter) - expectedY) <= 3.0,
							"浮窗应跟随块滚动（tagY=" + GetAdornerTagY(adornerAfter) + ", expected=" + expectedY + "）");

						// 浮窗仍是暂存浮窗 [Unstage]
						var buttonsAfter = CollectFloatingButtons(adornerAfter);
						Assert.Single(buttonsAfter);
						Assert.Equal(expectUnstage, buttonsAfter[0].Content);

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
			System.IO.File.WriteAllText("/tmp/fildediff_drift_b_wheel.txt", report);
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
			Assert.DoesNotContain("OUTER EXCEPTION", report);
		}

		// 按文本定位文档行 → 视口 Y（同 DiffSelectionFloatingButtonsTests 的定位方式）
		private static double YOf(AvaloniaEdit.Rendering.TextView textView, CommitCodeEditor editor, string lineText)
		{
			int offset = editor.Text.IndexOf(lineText, StringComparison.Ordinal);
			Assert.True(offset >= 0, "找不到测试行文本：" + lineText);
			int lineNumber = editor.Document.GetLineByOffset(offset).LineNumber;
			var vl = textView.GetVisualLine(lineNumber);
			Assert.True(vl != null, "行 " + lineNumber + "（" + lineText + "）无可视行");
			return vl.VisualTop - textView.ScrollOffset.Y + 6;
		}
	}
}
