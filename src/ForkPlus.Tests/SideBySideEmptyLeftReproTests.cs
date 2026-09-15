// 复现测试（2026-09-14，"Add 场景左空右有内容，拖右侧水平滚动条左侧有时不跟随"）：
// 左侧全 Alignment 空行（真实 extent≈0，canHorizontallyScroll 可能为 false），
// 右侧有长行。模拟连续拖动右侧水平滚动条，断言左侧逐步跟随。
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
	public class SideBySideEmptyLeftReproTests
	{
		/// <summary>纯 Add diff：无删除行，右（new）侧多行且含长行；左（old）侧只有 Alignment 空行。</summary>
		private static ForkPlus.Git.Diff.Diff MakeAddOnlyDiff(int lineCount, int longLineWidth)
		{
			var lines = new List<string>();
			lines.Add("ctx0\n");
			int addStart = lines.Count;
			for (int i = 0; i < lineCount; i++)
			{
				lines.Add((i == lineCount / 2 ? new string('w', longLineWidth) : "added line " + i) + "\n");
			}
			int tailStart = lines.Count;
			lines.Add("ctx tail\n");
			var subChunk = new SubChunk(
				new Range(0, 1),
				new Range(0, 0),
				new Range(addStart, addStart + lineCount),
				new Range(tailStart, tailStart + 1),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(1, lines.Count, 1, lines.Count, null, new[] { subChunk });
			return new ForkPlus.Git.Diff.Diff("new.txt", "new.txt", null, null, "", "222",
				lines.ToArray(), new[] { chunk }, null, ForkPlus.Git.Diff.Diff.FileType.Text, false);
		}

		private static ScrollViewer GetSv(DiffCodeEditor editor)
		{
			return editor.GetVisualDescendants().OfType<ScrollViewer>().First((ScrollViewer x) => x.Name == "PART_ScrollViewer");
		}

		[Fact]
		public void SideBySide_AddOnly_DragRightScrollbar_LeftFollowsEveryStep()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var control = new SideBySideTextDiffControl();
				var window = new Window { Width = 900, Height = 400, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				control.SetDiff(MakeAddOnlyDiff(60, 400), 4, entireFile: false, DiffLocation.Unstaged);
				Dispatcher.UIThread.RunJobs();
				try
				{
					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					TextView leftTextView = left.TextArea.TextView;
					TextView rightTextView = right.TextArea.TextView;
					ScrollViewer leftSv = GetSv(left);
					ScrollViewer rightSv = GetSv(right);

					// 前置诊断：左侧 extent 应已被同步器补宽到共享值
					double leftExtent = ((IScrollable)leftTextView).Extent.Width;
					double rightExtent = ((IScrollable)rightTextView).Extent.Width;

					// ===== 垂直滚动到长行区域（长行进入视口 → 右侧 extent 涨 → 共享宽涨）=====
					double maxRightY = ((IScrollable)rightTextView).Extent.Height - ((IScrollable)rightTextView).Viewport.Height;
					Assert.True(maxRightY > 100.0, "前置：应可垂直滚动（maxY=" + maxRightY.ToString("F1") + "）");
					rightSv.Offset = rightSv.Offset.WithY(maxRightY * 0.5);
					Dispatcher.UIThread.RunJobs();
					Assert.True(Math.Abs(leftTextView.ScrollOffset.Y - rightTextView.ScrollOffset.Y) <= 1.0,
						"垂直滚动后左右应同步：L=" + leftTextView.ScrollOffset.Y.ToString("F1")
						+ " R=" + rightTextView.ScrollOffset.Y.ToString("F1"));
					double rightExtentAfterV = ((IScrollable)rightTextView).Extent.Width;
					double leftExtentAfterV = ((IScrollable)leftTextView).Extent.Width;
					double maxRightX = rightExtentAfterV - ((IScrollable)rightTextView).Viewport.Width;
					Assert.True(maxRightX > 100.0,
						"前置：长行进入视口后右侧应有水平滚动范围（extent=" + rightExtentAfterV.ToString("F1")
						+ " max=" + maxRightX.ToString("F1") + "）");

					// 连续拖动右侧滚动条：10 步，每步推进 maxRightX/12（模拟真实拖拽的连续小步）
					for (int i = 1; i <= 10; i++)
					{
						double targetX = Math.Round(maxRightX * i / 12.0);
						rightSv.Offset = rightSv.Offset.WithX(targetX);
						Dispatcher.UIThread.RunJobs();
						double lx = leftTextView.ScrollOffset.X;
						double rx = rightTextView.ScrollOffset.X;
						Assert.True(Math.Abs(rx - targetX) <= 1.0,
							"第 " + i + " 步右侧未真实滚动：right=" + rx.ToString("F1") + "（期望 " + targetX.ToString("F1")
							+ "，extent L=" + leftExtentAfterV.ToString("F1") + " R=" + rightExtentAfterV.ToString("F1") + "）");
						Assert.True(Math.Abs(lx - rx) <= 1.0,
							"第 " + i + " 步失步：left=" + lx.ToString("F1") + " right=" + rx.ToString("F1")
							+ "（前置 extent L=" + leftExtent.ToString("F1") + " R=" + rightExtent.ToString("F1") + "）");
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
