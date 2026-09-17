// 探针 v2（2026-09-16，"hover 选中的对比块区域偏了 + 鼠标不在块上仍显示浮窗"）：
// v1 已证明 Split + 未滚动时 offset→hunk 映射正确。本探针补齐视觉/几何层：
//   A) Split + 长文件（需滚动）：滚轮滚动若幹次后逐可视行 hover，反射调用
//      GetRectForChunk 取层实际画的 Rect，校验"hover 行的视口 Y 区间 ⊆ Rect"。
//   B) SideBySide + 长文件 + CJK 行（触发行高同步器）：左右两栏逐行 hover，
//      校验本栏 Rect 与对栏（ActiveSiblingChunkView）Rect 的行对齐。
//   C) EntireFile=true：映射速查。
// 报告写入 %TEMP%\hover_chunk_probe2.txt。
using System;
using System.Collections.Generic;
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
	public class FileDiffHoverChunkRectProbeTests
	{
		private const int ChunkCount = 8;

		private static Diff MakeLongDiff(bool cjk)
		{
			var lines = new List<string>();
			var chunks = new List<Chunk>();
			for (int c = 0; c < ChunkCount; c++)
			{
				int pre = lines.Count;
				for (int i = 1; i <= 3; i++)
				{
					lines.Add(cjk ? $"第{c + 1}块上下文{i}行\n" : $"c{c + 1} ctx{i}\n");
				}
				int del = lines.Count;
				lines.Add(cjk ? $"第{c + 1}块被删除的行\n" : $"c{c + 1} deleted\n");
				int add = lines.Count;
				lines.Add(cjk ? $"第{c + 1}块新增的行内容\n" : $"c{c + 1} added\n");
				int post = lines.Count;
				for (int i = 1; i <= 5; i++)
				{
					lines.Add(cjk ? $"第{c + 1}块尾部上下文{i}\n" : $"c{c + 1} post{i}\n");
				}
				var sub = new SubChunk(
					new Range(pre, del), new Range(del, add), new Range(add, post),
					new Range(post, lines.Count), NoNewLineAtEndOfFile.None);
				chunks.Add(new Chunk(10 + c * 20, 6, 20 + c * 20, 7, null, new[] { sub }));
			}
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines.ToArray(),
				chunks.ToArray(), null, Diff.FileType.Text, false);
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

		private static object GetLayer(CommitCodeEditor editor)
		{
			return typeof(CommitCodeEditor).GetField("_diffSelectionLayer",
				BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);
		}

		private static object GetAdorner(object layer)
		{
			return typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_adorner",
				BindingFlags.NonPublic | BindingFlags.Instance).GetValue(layer);
		}

		private static Rect? GetRectForChunk(object layer, CommitDiffSelectedRange chunk)
		{
			var mi = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetMethod("GetRectForChunk",
				BindingFlags.NonPublic | BindingFlags.Instance);
			return (Rect?)mi.Invoke(layer, new object[] { chunk });
		}

		private static CommitDiffSelectedRange GetSiblingChunkView(object layer)
		{
			var prop = layer.GetType().GetProperty("ActiveSiblingChunkView",
				BindingFlags.NonPublic | BindingFlags.Instance);
			return (CommitDiffSelectedRange)prop.GetValue(layer);
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

		private static IEnumerable<int> VisibleLineNumbers(global::AvaloniaEdit.Rendering.TextView tv)
		{
			var vls = tv.VisualLines;
			if (vls == null)
			{
				return Enumerable.Empty<int>();
			}
			return vls.Select(v => v.FirstDocumentLine.LineNumber).Distinct().OrderBy(n => n);
		}

		// hover 一行并返回 (hover行, activeChunk, rect, adorner)
		private static void HoverLine(Window window, CommitCodeEditor editor, int docLine, out string desc)
		{
			var tv = editor.TextArea.TextView;
			var vl = tv.GetVisualLine(docLine);
			if (vl == null)
			{
				desc = "line " + docLine + " NOT VISUALIZED";
				return;
			}
			var origin = tv.TranslatePoint(new Point(0, 0), window) ?? new Point(0, 0);
			double y = vl.VisualTop - tv.ScrollOffset.Y + vl.Height / 2;
			HeadlessWindowExtensions.MouseMove(window, new Point(origin.X + 200, origin.Y + y),
				global::Avalonia.Input.RawInputModifiers.None);
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			HeadlessWindowExtensions.CaptureRenderedFrame(window);
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			var layer = GetLayer(editor);
			var active = editor.ActiveChunk;
			var adorner = GetAdorner(layer);
			Rect? rect = (active != null) ? GetRectForChunk(layer, active) : null;
			// hover 行的视口 Y 区间
			double lineTop = vl.VisualTop - tv.ScrollOffset.Y;
			double lineBottom = lineTop + vl.Height;
			bool contained = rect.HasValue
				&& rect.Value.Y <= lineTop + 1.5 && lineBottom <= rect.Value.Y + rect.Value.Height + 1.5;
			var docLineObj = editor.Document.GetLineByNumber(docLine);
			string text = editor.Document.GetText(docLineObj.Offset, docLineObj.Length).TrimEnd();
			desc = "hover " + docLine + " [" + text + "] active=" + DescribeChunk(active)
				+ " rect=" + (rect.HasValue ? (rect.Value.Y.ToString("F1") + "+" + rect.Value.Height.ToString("F1")) : "null")
				+ " lineY=" + lineTop.ToString("F1") + ".." + lineBottom.ToString("F1")
				+ (active != null ? (contained ? " CONTAINED" : " *** NOT-CONTAINED ***") : "")
				+ " adorner=" + (adorner != null);
		}

		private static void Wheel(Window window, Point p, int times)
		{
			for (int i = 0; i < times; i++)
			{
				HeadlessWindowExtensions.MouseWheel(window, p, new Vector(0, -120),
					global::Avalonia.Input.RawInputModifiers.None);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			}
		}

		[Fact]
		public void Probe_Split_Scrolled_Rect()
		{
			string report = RunWithTimeout(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					var control = new SplitCommitTextDiffControl();
					var window = new Window { Width = 900, Height = 400, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.SetDiff(MakeLongDiff(false), 4, false, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					var editor = (CommitCodeEditor)control.GetType().GetField("_editor",
						BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control);
					var tv = editor.TextArea.TextView;
					sb.AppendLine("doc lines=" + editor.Document.LineCount + ", viewport=" + editor.ViewportHeight);

					foreach (double target in new double[] { 0, 200, 500, 800 })
					{
						editor.SetScrollPosition(target);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
						sb.AppendLine("== target=" + target + " scrollOffset=" + tv.ScrollOffset.Y.ToString("F1")
							+ " caretLine=" + editor.TextArea.Caret.Line
							+ " visible " + VisibleLineNumbers(tv).Min() + ".." + VisibleLineNumbers(tv).Max());
						foreach (int line in VisibleLineNumbers(tv).ToArray())
						{
							string d;
							HoverLine(window, editor, line, out d);
							sb.AppendLine(d);
						}
					}
					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
				}
				return sb.ToString();
			}, 120000);
			System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hover_chunk_probe2.txt"), report);
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
		}

		[Fact]
		public void Probe_SideBySide_Scrolled_Rect()
		{
			string report = RunWithTimeout(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					var control = new SideBySideCommitTextDiffControl();
					var window = new Window { Width = 900, Height = 400, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					// CJK 内容：触发左右行高同步器
					control.SetDiff(MakeLongDiff(true), 4, false, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					var left = (CommitCodeEditor)control.GetType().GetField("_leftDiffCodeEditor",
						BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control);
					var right = (CommitCodeEditor)control.GetType().GetField("_rightDiffCodeEditor",
						BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control);
					var tvR = right.TextArea.TextView;
					sb.AppendLine("doc lines right=" + right.Document.LineCount);

					foreach (double target in new double[] { 0, 200, 500, 800 })
					{
						right.SetScrollPosition(target);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
						sb.AppendLine("== target=" + target + " rightScroll=" + tvR.ScrollOffset.Y.ToString("F1")
							+ " leftScroll=" + left.TextArea.TextView.ScrollOffset.Y.ToString("F1"));
						// 右栏（NEW）hover
						foreach (int line in VisibleLineNumbers(tvR).ToArray())
						{
							string d;
							HoverLine(window, right, line, out d);
							// 对栏（OLD）同步高亮块的 Rect
							var leftLayer = GetLayer(left);
							var sibView = GetSiblingChunkView(leftLayer);
							var sibRect = (sibView != null) ? GetRectForChunk(leftLayer, sibView) : null;
							d += " | siblingView=" + DescribeChunk(sibView)
								+ " sibRect=" + (sibRect.HasValue ? (sibRect.Value.Y.ToString("F1") + "+" + sibRect.Value.Height.ToString("F1")) : "null");
							sb.AppendLine(d);
						}
					}
					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
				}
				return sb.ToString();
			}, 120000);
			System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hover_chunk_probe3.txt"), report);
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
		}

		[Fact]
		public void Probe_EntireFile_Mapping()
		{
			string report = RunWithTimeout(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					var control = new SplitCommitTextDiffControl();
					var window = new Window { Width = 900, Height = 400, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					// entire-file：整文件模式（多 chunk 无 @@ 头时 git --unified=100000 会合成单 chunk，
					// 这里手工构造单 chunk 多 subchunk 的整文件形态）
					var lines = new List<string>();
					var subs = new List<SubChunk>();
					for (int c = 0; c < 8; c++)
					{
						int pre = lines.Count;
						for (int i = 1; i <= 6; i++)
						{
							lines.Add($"entire ctx{c + 1}-{i}\n");
						}
						int del = lines.Count;
						lines.Add($"entire del{c + 1}\n");
						int add = lines.Count;
						lines.Add($"entire add{c + 1}\n");
						int post = lines.Count;
						for (int i = 1; i <= 6; i++)
						{
							lines.Add($"entire tail{c + 1}-{i}\n");
						}
						subs.Add(new SubChunk(new Range(pre, del), new Range(del, add), new Range(add, post),
							new Range(post, lines.Count), NoNewLineAtEndOfFile.None));
					}
					var chunk = new Chunk(1, lines.Count, 1, lines.Count, null, subs.ToArray());
					var diff = new Diff("a.txt", "a.txt", null, null, "111", "222", lines.ToArray(),
						new[] { chunk }, null, Diff.FileType.Text, false);
					control.SetDiff(diff, 4, true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					var editor = (CommitCodeEditor)control.GetType().GetField("_editor",
						BindingFlags.NonPublic | BindingFlags.Instance).GetValue(control);
					var tv = editor.TextArea.TextView;
					sb.AppendLine("entire doc lines=" + editor.Document.LineCount);
					foreach (double target in new double[] { 0, 200, 500, 800 })
					{
						editor.SetScrollPosition(target);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
						sb.AppendLine("== target=" + target + " scrollOffset=" + tv.ScrollOffset.Y.ToString("F1"));
						foreach (int line in VisibleLineNumbers(tv).ToArray())
						{
							string d;
							HoverLine(window, editor, line, out d);
							sb.AppendLine(d);
						}
					}
					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
				}
				return sb.ToString();
			}, 120000);
			System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hover_chunk_probe4.txt"), report);
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
		}
	}
}
