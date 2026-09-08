// E2E 模块7（2026-09-05）：文本 Diff。
// 覆盖：Split ↔ SideBySide 布局切换（真实生产路径：FileControlHeader 的 DiffLayoutModeToggleButton
//   点击 → 设置 + NotificationCenter.DiffLayoutModeChanged → TextDiffControl.RefreshLayout 重建子控件、
//   diff 内容迁移）、SideBySide 双编辑器垂直滚动同步、水平滚动同步（横向滚动条弹动修复回归：
//   防抖 + 差值检查防联动循环）。
// 截图 → docs/evidence/e2e/07-textdiff/。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using ForkPlus.UI;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e07TextDiffTests
	{
		/// <summary>长行仓库：选中 wide.txt 并等待 working dir diff 装配（80 短行 + 400 字符长行）。</summary>
		private static CommitUserControl OpenCommitViewWithLongLineDiff(string repo, out MainWindow window)
		{
			RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out window);
			repoControl.ActivateCommitView();
			Dispatcher.UIThread.RunJobs();
			CommitUserControl commit = repoControl.Content.CommitUserControl;
			StageFileUserControl stage = commit.StageFileUserControl;
			Assert.True(UiClick.WaitFor(delegate
			{
				return stage.AllUnstagedFiles.Length == 1;
			}), "工作区状态未装配（1 个未暂存文件 wide.txt）");
			stage.UnstagedFilesFileListUserControl.SelectFile("wide.txt");
			Dispatcher.UIThread.RunJobs();
			Assert.True(UiClick.WaitFor(delegate
			{
				return commit.FileDiffControl.Content != null && commit.FileDiffControl.Content.Succeeded;
			}), "wide.txt 的 working dir diff 未加载");
			return commit;
		}

		[Fact]
		public void TextDiff_LayoutToggleButton_SwitchesSplitToSideBySide_AndSyncsVerticalScroll()
		{
			string repo = TestRepoFactory.CreateLongLines();
			DiffLayoutMode originalMode = ForkPlusSettings.Default.CommitDiffLayoutMode;
			try
			{
				// 显式从 Split 开始（防其他测试遗留 SideBySide 设置），后置恢复
				ForkPlusSettings.Default.CommitDiffLayoutMode = DiffLayoutMode.Split;
				HeadlessAppBootstrap.Run(delegate
				{
					CommitUserControl commit = OpenCommitViewWithLongLineDiff(repo, out var window);
					try
					{
						// ===== 1) Split 模式：单个编辑器 =====
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<CommitCodeEditor>(window).Count == 1;
						}), "Split 模式应只有 1 个 CommitCodeEditor");
						Assert.True(UiClick.FindAll<CommitCodeEditor>(window)[0].VisualPatch != null,
							"Split 编辑器应装配 VisualPatch");
						ScreenshotHelper.Snap(window, "01-split-mode", "07-textdiff");

						// ===== 2) 生产入口切换：diff 头部 DiffLayoutModeToggleButton 点击 =====
						FileControlHeaderUserControl header = UiClick.FindAll<FileControlHeaderUserControl>(window).First();
						ToggleButton layoutToggle = UiClick.Find<ToggleButton>(header, "DiffLayoutModeToggleButton");
						Assert.NotNull(layoutToggle);
						// 真实点击序：Button 先切 IsChecked 再 raise Click（UiClick.Click 只 raise 事件，先手动同步状态）
						layoutToggle.IsChecked = true;
						layoutToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
						Dispatcher.UIThread.RunJobs();

						// ===== 3) SideBySide 重建：两个编辑器（old 左 / new 右），内容迁移 =====
						var editors = UiClick.FindAll<CommitCodeEditor>(window);
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<CommitCodeEditor>(window).Count == 2;
						}), "SideBySide 模式应有 2 个 CommitCodeEditor");
						editors = UiClick.FindAll<CommitCodeEditor>(window);
						Assert.Equal(DiffViewMode.SideBySideOld, editors[0].DiffViewMode);
						Assert.Equal(DiffViewMode.SideBySideNew, editors[1].DiffViewMode);
						Assert.True(editors[0].VisualPatch != null, "切换后左侧编辑器应保留 VisualPatch（内容迁移）");
						Assert.True(editors[1].VisualPatch != null, "切换后右侧编辑器应保留 VisualPatch（内容迁移）");
						Assert.Equal(DiffLayoutMode.SideBySide, ForkPlusSettings.Default.CommitDiffLayoutMode);
						ScreenshotHelper.Snap(window, "02-side-by-side-mode", "07-textdiff");

						// ===== 4) 垂直滚动同步：右滚 → 左跟随（ScrollOffsetChanged → OnScrollOffsetChanged） =====
						// 注意：AvaloniaEdit 的 TextEditor.ScrollToVerticalOffset 是空操作（WpfCompat 注释有根因说明），
						// 生产同步走 PART_ScrollViewer.Offset——测试用同一入口 ScrollToVerticalOffsetCompat 驱动真实滚动。
						CommitCodeEditor left = editors[0];
						CommitCodeEditor right = editors[1];
						right.ScrollToVerticalOffsetCompat(100.0);
						Dispatcher.UIThread.RunJobs();
						double leftY = left.TextArea.TextView.ScrollOffset.Y;
						double rightY = right.TextArea.TextView.ScrollOffset.Y;
						Assert.True(Math.Abs(leftY - rightY) < 1.0,
							"右侧垂直滚动后左侧应同步（left=" + leftY.ToString("F1") + " right=" + rightY.ToString("F1") + "）");
						Assert.True(rightY > 0.0, "右侧应真实滚动（offset=" + rightY.ToString("F1") + "，文档应有垂直滚动范围）");
						ScreenshotHelper.Snap(window, "03-vertical-scroll-synced", "07-textdiff");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				// 恢复后必须落盘：测试期间 harness（CloseRepositoryTab→SaveSession）会把修改值
				// 持久化到用户 settings.json——只恢复内存不落盘会污染后续所有测试运行
				//（实证：E2e05 行级测试在 SideBySide 残留下拿到左侧旧内容编辑器而断言失败）。
				ForkPlusSettings.Default.CommitDiffLayoutMode = originalMode;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void TextDiff_SideBySideHorizontalScroll_SyncsAndAlternatingScrollsDontBounce()
		{
			string repo = TestRepoFactory.CreateLongLines();
			DiffLayoutMode originalMode = ForkPlusSettings.Default.CommitDiffLayoutMode;
			try
			{
				ForkPlusSettings.Default.CommitDiffLayoutMode = DiffLayoutMode.SideBySide;
				HeadlessAppBootstrap.Run(delegate
				{
					CommitUserControl commit = OpenCommitViewWithLongLineDiff(repo, out var window);
					try
					{
						var editors = UiClick.FindAll<CommitCodeEditor>(window);
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<CommitCodeEditor>(window).Count == 2;
						}), "SideBySide 模式应有 2 个 CommitCodeEditor");
						editors = UiClick.FindAll<CommitCodeEditor>(window);
						CommitCodeEditor left = editors[0];
						CommitCodeEditor right = editors[1];

						// ===== 1) 水平滚动同步：右滚 120px → 左跟随（横向滚动条联动） =====
						right.ScrollToHorizontalOffsetCompat(120.0);
						Dispatcher.UIThread.RunJobs();
						double leftX = left.TextArea.TextView.ScrollOffset.X;
						double rightX = right.TextArea.TextView.ScrollOffset.X;
						Assert.True(rightX > 0.0, "右侧应真实水平滚动（offset=" + rightX.ToString("F1") + "，长行应有水平滚动范围）");
						Assert.True(Math.Abs(leftX - rightX) < 1.0,
							"右侧水平滚动后左侧应同步（left=" + leftX.ToString("F1") + " right=" + rightX.ToString("F1") + "）");
						ScreenshotHelper.Snap(window, "04-horizontal-scroll-synced", "07-textdiff");

						// ===== 2) 弹动回归（2026-09-05"点击横向滚动条界面弹动"修复）：
						// 防抖窗口内交替左右滚动——差值检查 + 100ms 防抖应阻止联动循环，
						// 最终偏移收敛一致且过程不抛异常（循环会以 StackOverflow/卡死形式暴露）。
						left.ScrollToHorizontalOffsetCompat(200.0);
						Dispatcher.UIThread.RunJobs();
						for (int i = 0; i < 5; i++)
						{
							right.ScrollToHorizontalOffsetCompat(150.0 + i * 10.0);
							Dispatcher.UIThread.RunJobs();
							left.ScrollToHorizontalOffsetCompat(160.0 + i * 10.0);
							Dispatcher.UIThread.RunJobs();
						}
						// 收敛断言：交替滚动停止后两侧偏移一致（最后一次同步生效）
						double finalLeft = left.TextArea.TextView.ScrollOffset.X;
						double finalRight = right.TextArea.TextView.ScrollOffset.X;
						Assert.True(Math.Abs(finalLeft - finalRight) <= 10.0,
							"交替滚动后两侧偏移应收敛（left=" + finalLeft.ToString("F1") + " right=" + finalRight.ToString("F1") + "）");
						Assert.True(finalLeft > 0.0 && finalRight > 0.0, "交替滚动后应有非零偏移");

						// ===== 3) 垂直滚动来回（弹动回归的垂直轴同样防抖） =====
						for (int i = 0; i < 3; i++)
						{
							right.ScrollToVerticalOffsetCompat(80.0 + i * 20.0);
							Dispatcher.UIThread.RunJobs();
							left.ScrollToVerticalOffsetCompat(90.0 + i * 20.0);
							Dispatcher.UIThread.RunJobs();
						}
						Assert.True(Math.Abs(left.TextArea.TextView.ScrollOffset.Y - right.TextArea.TextView.ScrollOffset.Y) <= 20.0,
							"垂直交替滚动后偏移应收敛");
						ScreenshotHelper.Snap(window, "05-alternating-scroll-no-bounce", "07-textdiff");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.CommitDiffLayoutMode = originalMode;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
