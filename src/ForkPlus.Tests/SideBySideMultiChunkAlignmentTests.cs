// 诊断测试（2026-09-14，"FileDiff 有时候左右视图滚动条不同步"）：多 SubChunk（多变更块）
// 场景下两侧文档高度/最大偏移是否恒等——不等的块对齐缺口会让一侧提前到底、
// 末端区间两侧失步（短侧定格、长侧继续滚），表现为"滚动条不同步"。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Rendering;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls.Editor.Diff;
using Xunit;
using Range = ForkPlus.Range;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class SideBySideMultiChunkAlignmentTests
	{
		/// <summary>多块 diff：3 个 SubChunk（删除 2 行 / 新增 3 行 / 改 1 行），块间各 2 行上下文。</summary>
		private static ForkPlus.Git.Diff.Diff MakeMultiChunkDiff(int fillerBlocks)
		{
			var lines = new List<string>();
			lines.Add("ctx0\n");
			int d1Start = lines.Count;
			lines.Add("del1\n");
			lines.Add("del2\n");
			int a1Start = lines.Count;
			lines.Add("add1\n");
			lines.Add("add2\n");
			lines.Add("add3\n");
			int ctx1Start = lines.Count;
			lines.Add("ctx1\n");
			lines.Add("ctx2\n");
			int d2Start = lines.Count;
			lines.Add("changed-old\n");
			int a2Start = lines.Count;
			lines.Add("changed-new\n");
			int tailStart = lines.Count;
			lines.Add("ctx3\n");
			for (int i = 0; i < fillerBlocks * 20; i++)
			{
				lines.Add("filler " + i + "\n");
			}
			var chunks = new List<SubChunk>
			{
				new SubChunk(new Range(0, 1), new Range(d1Start, d1Start + 2), new Range(a1Start, a1Start + 3), new Range(ctx1Start, ctx1Start + 2), NoNewLineAtEndOfFile.None),
				new SubChunk(new Range(ctx1Start, ctx1Start + 2), new Range(d2Start, d2Start + 1), new Range(a2Start, a2Start + 1), new Range(tailStart, lines.Count), NoNewLineAtEndOfFile.None),
			};
			var chunk = new Chunk(1, lines.Count, 1, lines.Count, null, chunks.ToArray());
			return new ForkPlus.Git.Diff.Diff("m.txt", "m.txt", null, null, "111", "222",
				lines.ToArray(), new[] { chunk }, null, ForkPlus.Git.Diff.Diff.FileType.Text, false);
		}

		private static string Extents(DiffCodeEditor l, DiffCodeEditor r)
		{
			var le = ((IScrollable)l.TextArea.TextView).Extent;
			var re = ((IScrollable)r.TextArea.TextView).Extent;
			var lv = ((IScrollable)l.TextArea.TextView).Viewport;
			var rv = ((IScrollable)r.TextArea.TextView).Viewport;
			return "L extent=" + le.Height.ToString("F1") + " viewport=" + lv.Height.ToString("F1")
				+ " max=" + (le.Height - lv.Height).ToString("F1")
				+ " | R extent=" + re.Height.ToString("F1") + " viewport=" + rv.Height.ToString("F1")
				+ " max=" + (re.Height - rv.Height).ToString("F1");
		}

		[Fact]
		public void SideBySide_MultiChunk_BothSidesHaveEqualVerticalRange()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var control = new SideBySideTextDiffControl();
				var window = new Window { Width = 900, Height = 400, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					foreach (bool entire in new[] { false, true })
					{
						control.SetDiff(MakeMultiChunkDiff(3), 4, entire, DiffLocation.Unstaged);
						Dispatcher.UIThread.RunJobs();
						DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
							.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
						DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
							.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
						double leftMax = ((IScrollable)left.TextArea.TextView).Extent.Height - ((IScrollable)left.TextArea.TextView).Viewport.Height;
						double rightMax = ((IScrollable)right.TextArea.TextView).Extent.Height - ((IScrollable)right.TextArea.TextView).Viewport.Height;
						Assert.True(Math.Abs(leftMax - rightMax) <= 1.0,
							"entireFile=" + entire + "：两侧垂直 max 应相等（多块对齐无缺口）。" + Extents(left, right));

						// 滚到 3/4 处与末端再验证双侧同步
						ScrollViewer leftSv = left.GetVisualDescendants().OfType<ScrollViewer>().First((ScrollViewer x) => x.Name == "PART_ScrollViewer");
						ScrollViewer rightSv = right.GetVisualDescendants().OfType<ScrollViewer>().First((ScrollViewer x) => x.Name == "PART_ScrollViewer");
						leftSv.Offset = new Vector(0.0, leftMax * 0.75);
						Dispatcher.UIThread.RunJobs();
						Assert.True(Math.Abs(left.TextArea.TextView.ScrollOffset.Y - right.TextArea.TextView.ScrollOffset.Y) <= 1.0,
							"entireFile=" + entire + "：滚到 75% 失步 L=" + left.TextArea.TextView.ScrollOffset.Y.ToString("F1") + " R=" + right.TextArea.TextView.ScrollOffset.Y.ToString("F1"));
						rightSv.Offset = new Vector(0.0, rightMax);
						Dispatcher.UIThread.RunJobs();
						Assert.True(Math.Abs(left.TextArea.TextView.ScrollOffset.Y - right.TextArea.TextView.ScrollOffset.Y) <= 1.0,
							"entireFile=" + entire + "：滚到底失步 L=" + left.TextArea.TextView.ScrollOffset.Y.ToString("F1") + " R=" + right.TextArea.TextView.ScrollOffset.Y.ToString("F1"));
					}
				}
				finally
				{
					window.Close();
				}
			}).GetAwaiter().GetResult();
		}
	}
}
