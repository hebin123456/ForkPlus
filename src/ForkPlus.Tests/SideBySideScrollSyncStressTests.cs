// 诊断测试（2026-09-14，"FileDiff 有时候左右视图滚动条不同步"）：模拟真实使用序列
//（垂直滚动到文件末端、横滚交替、SetDiff 复用控件切换内容、窗口 resize），每步断言
// 双轴像素级一致，定位间歇性失步的场景与阶段。
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
	public class SideBySideScrollSyncStressTests
	{
		/// <summary>构造大文档 diff：totalLines 行，leftWideAt/leftWideWidth 控制某侧长行位置。</summary>
		private static ForkPlus.Git.Diff.Diff MakeDiff(int totalLines, int longLineWidth)
		{
			var lines = new List<string>();
			lines.Add("header context\n");
			int delStart = lines.Count;
			lines.Add(new string('w', Math.Max(10, longLineWidth)) + "\n");
			int addStart = lines.Count;
			lines.Add(new string('n', Math.Max(10, longLineWidth / 2)) + "\n");
			while (lines.Count < totalLines)
			{
				lines.Add("filler context line " + lines.Count + "\n");
			}
			var subChunk = new SubChunk(
				new Range(0, 1),
				new Range(delStart, delStart + 1),
				new Range(addStart, addStart + 1),
				new Range(2, lines.Count),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(1, lines.Count, 1, lines.Count, null, new[] { subChunk });
			return new ForkPlus.Git.Diff.Diff("a.txt", "a.txt", null, null, "111", "222",
				lines.ToArray(), new[] { chunk }, null, ForkPlus.Git.Diff.Diff.FileType.Text, false);
		}

		private static ScrollViewer GetSv(DiffCodeEditor editor)
		{
			return editor.GetVisualDescendants().OfType<ScrollViewer>().First((ScrollViewer x) => x.Name == "PART_ScrollViewer");
		}

		private static string Describe(DiffCodeEditor left, DiffCodeEditor right)
		{
			var l = left.TextArea.TextView.ScrollOffset;
			var r = right.TextArea.TextView.ScrollOffset;
			return "L=(" + l.X.ToString("F1") + "," + l.Y.ToString("F1") + ") R=(" + r.X.ToString("F1") + "," + r.Y.ToString("F1") + ")";
		}

		private static void AssertAligned(DiffCodeEditor left, DiffCodeEditor right, string stage)
		{
			var l = left.TextArea.TextView.ScrollOffset;
			var r = right.TextArea.TextView.ScrollOffset;
			Assert.True(Math.Abs(l.X - r.X) <= 1.0, stage + "：水平失步 " + Describe(left, right));
			Assert.True(Math.Abs(l.Y - r.Y) <= 1.0, stage + "：垂直失步 " + Describe(left, right));
		}

		[Fact]
		public void SideBySide_RealWorldSequence_StaysSynced()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var control = new SideBySideTextDiffControl();
				var window = new Window { Width = 900, Height = 400, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
					.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
				DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
					.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
				try
				{
					// ===== 阶段 1：大文档（300 行），垂直滚动全旅程 + 每站断言 =====
					control.SetDiff(MakeDiff(300, 200), 4, entireFile: true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();
					ScrollViewer leftSv = GetSv(left);
					ScrollViewer rightSv = GetSv(right);
					double maxLeftY = ((IScrollable)left.TextArea.TextView).Extent.Height - ((IScrollable)left.TextArea.TextView).Viewport.Height;
					double maxRightY = ((IScrollable)right.TextArea.TextView).Extent.Height - ((IScrollable)right.TextArea.TextView).Viewport.Height;
					Assert.True(Math.Abs(maxLeftY - maxRightY) <= 1.0,
						"垂直 max 应相等：left=" + maxLeftY.ToString("F1") + " right=" + maxRightY.ToString("F1"));

					foreach (double frac in new[] { 0.25, 0.5, 0.75, 1.0 })
					{
						leftSv.Offset = new Vector(0.0, maxLeftY * frac);
						Dispatcher.UIThread.RunJobs();
						AssertAligned(left, right, "阶段1 左滚 frac=" + frac.ToString("F2"));
						rightSv.Offset = new Vector(0.0, Math.Max(0.0, maxRightY * frac - 30.0));
						Dispatcher.UIThread.RunJobs();
						AssertAligned(left, right, "阶段1 右回滚 frac=" + frac.ToString("F2"));
					}

					// ===== 阶段 2：滚到底后横滚（长行在顶部已滚出视口）=====
					leftSv.Offset = new Vector(150.0, maxLeftY);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段2 底部横滚 150");
					rightSv.Offset = new Vector(270.0, maxLeftY);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段2 底部右滚 270");

					// ===== 阶段 3：SetDiff 复用控件换内容（更长更宽）后继续滚 =====
					control.SetDiff(MakeDiff(500, 400), 4, entireFile: true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();
					leftSv.Offset = new Vector(120.0, 0.0);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段3 换内容后左滚");
					double newMaxY = ((IScrollable)left.TextArea.TextView).Extent.Height - ((IScrollable)left.TextArea.TextView).Viewport.Height;
					rightSv.Offset = new Vector(120.0, newMaxY);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段3 右滚到新底部");
					leftSv.Offset = new Vector(300.0, newMaxY * 0.5);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段3 左滚中部+横滚");

					// ===== 阶段 4：窗口 resize（viewport 变化）后滚动 =====
					window.Width = 1200;
					window.Height = 500;
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段4 resize 静止");
					double resizedMaxY = ((IScrollable)left.TextArea.TextView).Extent.Height - ((IScrollable)left.TextArea.TextView).Viewport.Height;
					leftSv.Offset = new Vector(80.0, Math.Min(resizedMaxY, 500.0));
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段4 resize 后左滚");
					rightSv.Offset = new Vector(200.0, 200.0);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段4 resize 后右滚");

					// ===== 阶段 5：换小内容（extent 大幅缩小，IsLogicalScrollEnabled 可能翻转）=====
					control.SetDiff(MakeDiff(8, 30), 4, entireFile: true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段5 小内容静止");
					// 小内容 + 大窗口：水平/垂直都可能不需要滚（max=0），仍不得失步
					leftSv.Offset = new Vector(0.0, 0.0);
					rightSv.Offset = new Vector(0.0, 0.0);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段5 小内容归零");

					// ===== 阶段 6：再换回大内容（false→true 翻转后）滚动 =====
					control.SetDiff(MakeDiff(300, 200), 4, entireFile: true, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();
					double max6 = ((IScrollable)left.TextArea.TextView).Extent.Height - ((IScrollable)left.TextArea.TextView).Viewport.Height;
					Assert.True(max6 > 100.0, "阶段6 前置：应可垂直滚动（max=" + max6.ToString("F1") + "）");
					leftSv.Offset = new Vector(100.0, max6 * 0.6);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段6 翻转后左滚");
					rightSv.Offset = new Vector(220.0, max6 * 0.3);
					Dispatcher.UIThread.RunJobs();
					AssertAligned(left, right, "阶段6 翻转后右滚");
				}
				finally
				{
					window.Close();
				}
			}).GetAwaiter().GetResult();
		}
	}
}
