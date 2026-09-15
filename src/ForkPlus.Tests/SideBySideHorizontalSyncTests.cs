// 回归测试（2026-09-14，"左右滚动 diff 对齐"修复产物）：
// 根因（反编译 AvaloniaEdit 12.0.0 TextView 实证）：TextView.MeasureOverride 里
// _scrollExtent.Width = 可见行最大宽度 + 3（只构建视口内的行），导致：
//   1) 两侧水平滚动范围天然不等——一侧有长行另一侧没有时，窄侧 max≈0，
//      ClampHorizontalOffsetToDocumentArea 把同步目标钳到 0 → 两栏列完全错位；
//   2) 范围随垂直滚动实时变化——长行进出视口时两侧 extent 各自塌缩，
//      ArrangeOverride 内部钳制让水平偏移各自跳动回 0；
//   3) 旧同步的"2s 内 40 次写入 → 熔断挂起 5s"会把用户快速横滚误判为回声链，
//      联动暂停 → 两栏漂移。
// 修复：SideBySideExtentSynchronizer 把两侧 _scrollExtent.Width 统一抬到两侧最大
// 可见宽度（单调不缩，窄侧可滚入空白区）；同步回声改为"写入值匹配"确定性消费，
// 移除时间防抖/熔断。
// 本测试守卫：范围共享、横滚像素对齐、垂直滚动不引发水平跳动、快速交替滚动不熔断。
using System;
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
	public class SideBySideHorizontalSyncTests
	{
		/// <summary>左侧（old）一条 300 字符长行 + 1 条短 added，其余全短 context：
		/// 左侧有水平滚动范围、右侧（修复前）没有——错位的典型场景。</summary>
		private static ForkPlus.Git.Diff.Diff MakeAsymmetricWidthDiff()
		{
			var lines = new System.Collections.Generic.List<string>();
			for (int i = 0; i < 3; i++)
			{
				lines.Add("context line " + i + "\n");
			}
			int deletedStart = lines.Count;
			lines.Add(new string('x', 300) + "\n");
			int addedStart = lines.Count;
			lines.Add("short added\n");
			int postStart = lines.Count;
			for (int i = 0; i < 50; i++)
			{
				lines.Add("post context line " + i + "\n");
			}
			var subChunk = new SubChunk(
				new Range(0, 3),
				new Range(deletedStart, deletedStart + 1),
				new Range(addedStart, addedStart + 1),
				new Range(postStart, lines.Count),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(1, lines.Count, 1, lines.Count, null, new[] { subChunk });
			return new ForkPlus.Git.Diff.Diff("a.txt", "a.txt", null, null, "111", "222", lines.ToArray(), new[] { chunk }, null, ForkPlus.Git.Diff.Diff.FileType.Text, false);
		}

		private static ScrollViewer GetEditorScrollViewer(DiffCodeEditor editor)
		{
			return editor.GetVisualDescendants().OfType<ScrollViewer>().First((ScrollViewer x) => x.Name == "PART_ScrollViewer");
		}

		[Fact]
		public void SideBySide_AsymmetricLineWidths_ShareScrollRangeAndStayColumnAligned()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var control = new SideBySideTextDiffControl();
				var window = new Window { Width = 900, Height = 400, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				control.SetDiff(MakeAsymmetricWidthDiff(), 4, entireFile: true, DiffLocation.Unstaged);
				Dispatcher.UIThread.RunJobs();
				try
				{
					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					TextView leftTextView = left.TextArea.TextView;
					TextView rightTextView = right.TextArea.TextView;

					// ===== 1) 两侧水平 extent 应被同步器抬到共同最大值（修复前右侧 ≈ 短行宽 ≈ 0 可滚） =====
					double leftExtent = ((IScrollable)leftTextView).Extent.Width;
					double rightExtent = ((IScrollable)rightTextView).Extent.Width;
					Assert.True(leftExtent > 1500.0,
						"左侧 300 字符长行应在视口内，extent 应很宽（实际 " + leftExtent.ToString("F1") + "）");
					Assert.True(Math.Abs(leftExtent - rightExtent) <= 1.0,
						"两侧水平 extent 应相等：left=" + leftExtent.ToString("F1") + " right=" + rightExtent.ToString("F1"));

					// ===== 2) 滚宽侧 → 窄侧像素级跟随（修复前窄侧被钳在 0，列完全错位） =====
					ScrollViewer leftSv = GetEditorScrollViewer(left);
					leftSv.Offset = leftSv.Offset.WithX(120.0);
					Dispatcher.UIThread.RunJobs();
					Assert.True(Math.Abs(leftTextView.ScrollOffset.X - 120.0) < 1.5,
						"左侧应真实横滚（实际 " + leftTextView.ScrollOffset.X.ToString("F1") + "）");
					Assert.True(Math.Abs(rightTextView.ScrollOffset.X - 120.0) < 1.5,
						"右侧应像素级跟随：right=" + rightTextView.ScrollOffset.X.ToString("F1") + "（期望 ~120）");

					// ===== 3) 反向：滚窄侧（修复前窄侧无滚动范围，根本滚不动） =====
					ScrollViewer rightSv = GetEditorScrollViewer(right);
					rightSv.Offset = rightSv.Offset.WithX(260.0);
					Dispatcher.UIThread.RunJobs();
					Assert.True(Math.Abs(rightTextView.ScrollOffset.X - 260.0) < 1.5,
						"右侧（窄侧）应能滚入共享范围（实际 " + rightTextView.ScrollOffset.X.ToString("F1") + "）");
					Assert.True(Math.Abs(leftTextView.ScrollOffset.X - 260.0) < 1.5,
						"左侧应跟随右侧：left=" + leftTextView.ScrollOffset.X.ToString("F1") + "（期望 ~260）");

					// ===== 4) 滚回 0 两侧复位 =====
					rightSv.Offset = rightSv.Offset.WithX(0.0);
					Dispatcher.UIThread.RunJobs();
					Assert.True(Math.Abs(leftTextView.ScrollOffset.X) < 1.5 && Math.Abs(rightTextView.ScrollOffset.X) < 1.5,
						"滚回 0 后两侧都应复位");
				}
				finally
				{
					window.Close();
				}
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void SideBySide_VerticalScroll_DoesNotDisturbHorizontalAlignment()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var control = new SideBySideTextDiffControl();
				var window = new Window { Width = 900, Height = 400, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				control.SetDiff(MakeAsymmetricWidthDiff(), 4, entireFile: true, DiffLocation.Unstaged);
				Dispatcher.UIThread.RunJobs();
				try
				{
					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					TextView leftTextView = left.TextArea.TextView;
					TextView rightTextView = right.TextArea.TextView;

					// 先横滚对齐到 120px
					ScrollViewer leftSv = GetEditorScrollViewer(left);
					leftSv.Offset = leftSv.Offset.WithX(120.0);
					Dispatcher.UIThread.RunJobs();
					Assert.True(Math.Abs(rightTextView.ScrollOffset.X - 120.0) < 1.5, "前置：右侧应已跟随到 120");

					// 垂直滚动：300 字符长行（第 4 行）滚出视口。修复前两侧 extent 各自
					// 塌缩到短行宽 → ArrangeOverride 内部钳制把水平偏移拉回 0 → 列错位。
					leftSv.Offset = leftSv.Offset.WithY(600.0);
					Dispatcher.UIThread.RunJobs();

					Assert.True(Math.Abs(leftTextView.ScrollOffset.X - 120.0) < 1.5,
						"垂直滚动后左侧水平偏移应保持（实际 " + leftTextView.ScrollOffset.X.ToString("F1") + "）");
					Assert.True(Math.Abs(rightTextView.ScrollOffset.X - 120.0) < 1.5,
						"垂直滚动后右侧水平偏移应保持（实际 " + rightTextView.ScrollOffset.X.ToString("F1") + "）");
					// 单调共享：长行滚出视口后范围不得塌缩
					Assert.True(((IScrollable)rightTextView).Extent.Width > 1500.0,
						"长行滚出视口后右侧 extent 不应塌缩（实际 " + ((IScrollable)rightTextView).Extent.Width.ToString("F1") + "）");
				}
				finally
				{
					window.Close();
				}
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void SideBySide_FastAlternatingScrolls_StaySyncedWithoutSuspension()
		{
			// 旧熔断器：2s 内同步写入超 40 次 → 暂停联动 5s。60 轮交替滚动（120+ 次写入，
			// 远超 40）在旧方案下必然触发熔断 → 两侧漂移。新方案按写入值匹配消费回声，
			// 无熔断，快速交替滚动每一步都立即互相跟随并收敛。
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var control = new SideBySideTextDiffControl();
				var window = new Window { Width = 900, Height = 400, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				control.SetDiff(MakeAsymmetricWidthDiff(), 4, entireFile: true, DiffLocation.Unstaged);
				Dispatcher.UIThread.RunJobs();
				try
				{
					DiffCodeEditor left = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideOld);
					DiffCodeEditor right = control.GetVisualDescendants().OfType<DiffCodeEditor>()
						.First((DiffCodeEditor e) => e.DiffViewMode == DiffViewMode.SideBySideNew);
					ScrollViewer leftSv = GetEditorScrollViewer(left);
					ScrollViewer rightSv = GetEditorScrollViewer(right);

					for (int i = 0; i < 60; i++)
					{
						leftSv.Offset = new Vector(50.0 + i, 0.0);
						Dispatcher.UIThread.RunJobs();
						rightSv.Offset = new Vector(60.0 + i, 0.0);
						Dispatcher.UIThread.RunJobs();
					}
					double leftX = left.TextArea.TextView.ScrollOffset.X;
					double rightX = right.TextArea.TextView.ScrollOffset.X;
					Assert.True(Math.Abs(leftX - rightX) <= 1.0,
						"60 轮快速交替滚动后两侧应收敛一致：left=" + leftX.ToString("F1") + " right=" + rightX.ToString("F1"));
					Assert.True(Math.Abs(leftX - 119.0) <= 1.0,
						"最终偏移应为最后一轮的值 119（实际 " + leftX.ToString("F1") + "）");
				}
				finally
				{
					window.Close();
				}
			}).GetAwaiter().GetResult();
		}
	}
}
