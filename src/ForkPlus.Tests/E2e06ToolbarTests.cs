// E2E 模块6（2026-09-05）：工具栏。
// 覆盖：无仓库 tab 时全部 git 操作按钮禁用 + 角标隐藏、打开无 upstream 仓库时按钮启用 +
//   角标隐藏、ahead/behind 仓库 PullBadge/PushBadge 角标数字与定位（真实 git ahead-behind
//   管线：RefreshRepositoryData → UpdateRepositoryData → RefreshToolbarBadges）。
// 截图 → docs/evidence/e2e/06-toolbar/。
using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e06ToolbarTests
	{
		/// <summary>等待仓库数据装配完成（UpdateRepositoryData 已跑 → 工具栏按钮/角标已刷新）。</summary>
		private static void WaitForRepositoryData(RepositoryUserControl repoControl)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return repoControl.RepositoryData != null;
			}), "RepositoryData 未在 15s 内装配完成");
			Dispatcher.UIThread.RunJobs();
		}

		[Fact]
		public void Toolbar_NoRepository_AllGitButtonsDisabledAndBadgesHidden()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				MainWindow window = E2eMainWindowHarness.CreateWindow();
				try
				{
					ToolbarUserControl toolbar = window.Toolbar;
					// 无活动仓库（仓库管理 tab）：全部 git 操作入口禁用（RefreshToolbar 的 isEnabled=false 分支）
					Assert.False(toolbar.FetchToolbarButton.IsEnabled, "Fetch 按钮应禁用");
					Assert.False(toolbar.PullToolbarButton.IsEnabled, "Pull 按钮应禁用");
					Assert.False(toolbar.PushToolbarButton.IsEnabled, "Push 按钮应禁用");
					Assert.False(toolbar.StashToolbarButton.IsEnabled, "Stash 按钮应禁用");
					Assert.False(toolbar.StashToolbarDropdownButton.IsEnabled, "Stash 下拉应禁用");
					Assert.False(toolbar.BranchToolbarButton.IsEnabled, "Branch 按钮应禁用");
					Assert.False(toolbar.BranchToolbarDropdownButton.IsEnabled, "Branch 下拉应禁用");
					Assert.False(toolbar.OpenInDropDownButton.IsEnabled, "Open in 按钮应禁用");
					Assert.False(toolbar.OpenInConsoleToolbarButton.IsEnabled, "控制台按钮应禁用");
					Assert.False(toolbar.AiDevelopmentToolbarButton.IsEnabled, "AI 按钮应禁用");
					// 无 upstream：两个角标都隐藏
					Assert.False(toolbar.PullBadge.IsVisible, "无仓库时 PullBadge 应隐藏");
					Assert.False(toolbar.PushBadge.IsVisible, "无仓库时 PushBadge 应隐藏");
					// Undo/Redo 可见性跟随设置（默认开），无仓库时禁用
					Assert.Equal(ForkPlusSettings.Default.UndoRedoEnabled, toolbar.UndoToolbarButton.IsVisible);
					Assert.False(toolbar.UndoToolbarButton.IsEnabled, "无仓库时 Undo 应禁用");
					Assert.False(toolbar.RedoToolbarButton.IsEnabled, "无仓库时 Redo 应禁用");

					ScreenshotHelper.Snap(window, "01-toolbar-no-repo-disabled", "06-toolbar");
				}
				finally
				{
					E2eMainWindowHarness.DetachWindow(window);
				}
			});
		}

		[Fact]
		public void Toolbar_RepoWithoutUpstream_ButtonsEnabledBadgesHidden()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForRepositoryData(repoControl);
						ToolbarUserControl toolbar = window.Toolbar;
						// 有活动仓库：全部 git 操作入口启用
						Assert.True(toolbar.FetchToolbarButton.IsEnabled, "Fetch 按钮应启用");
						Assert.True(toolbar.PullToolbarButton.IsEnabled, "Pull 按钮应启用");
						Assert.True(toolbar.PushToolbarButton.IsEnabled, "Push 按钮应启用");
						Assert.True(toolbar.StashToolbarButton.IsEnabled, "Stash 按钮应启用");
						Assert.True(toolbar.StashToolbarDropdownButton.IsEnabled, "Stash 下拉应启用");
						Assert.True(toolbar.BranchToolbarButton.IsEnabled, "Branch 按钮应启用");
						Assert.True(toolbar.BranchToolbarDropdownButton.IsEnabled, "Branch 下拉应启用");
						Assert.True(toolbar.OpenInDropDownButton.IsEnabled, "Open in 按钮应启用");
						Assert.True(toolbar.OpenInConsoleToolbarButton.IsEnabled, "控制台按钮应启用");
						Assert.True(toolbar.AiDevelopmentToolbarButton.IsEnabled, "AI 按钮应启用");
						Assert.True(toolbar.ReflogToolbarButton.IsEnabled, "Reflog 按钮有活动仓库即启用");
						// CreateBasic 无远程无 upstream → GetUpstreamStatus 无效 → 角标隐藏
						Assert.False(toolbar.PullBadge.IsVisible, "无 upstream 时 PullBadge 应隐藏");
						Assert.False(toolbar.PushBadge.IsVisible, "无 upstream 时 PushBadge 应隐藏");

						ScreenshotHelper.Snap(window, "02-toolbar-repo-no-upstream", "06-toolbar");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void Toolbar_AheadBehindRepo_PullPushBadgesShowCountsAndPosition()
		{
			string repo = TestRepoFactory.CreateAheadBehind();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForRepositoryData(repoControl);
						ToolbarUserControl toolbar = window.Toolbar;
						// ahead 2 / behind 1 → PushBadge "2"、PullBadge "1"（RefreshPullPushBadges → RefreshBadge）
						bool badgesShown = UiClick.WaitFor(delegate
						{
							return toolbar.PullBadge.IsVisible && toolbar.PushBadge.IsVisible;
						});
						Assert.True(badgesShown, "ahead/behind 仓库两个角标都应显示（UpdateRepositoryData → RefreshToolbarBadges）");
						Assert.Equal("2", toolbar.PushBadgeText.Text); // ahead 2
						Assert.Equal("1", toolbar.PullBadgeText.Text); // behind 1
						// 角标定位在按钮右上（RefreshBadgePosition：button 右缘 -10、顶部 -2）
						double pushLeft = Avalonia.Controls.Canvas.GetLeft(toolbar.PushBadge);
						double pushTop = Avalonia.Controls.Canvas.GetTop(toolbar.PushBadge);
						Assert.True(pushLeft > 0.0, "PushBadge 应被定位到按钮右上（Canvas.Left=" + pushLeft + "）");
						Assert.True(pushTop >= -2.0, "PushBadge Canvas.Top 应 ≈ 按钮顶部（实际 " + pushTop + "）");
						// 角标不与下拉按钮重叠：Push 角标在 Pull 角标右侧（按钮顺序 Fetch→Pull→Push）
						double pullLeft = Avalonia.Controls.Canvas.GetLeft(toolbar.PullBadge);
						Assert.True(pushLeft > pullLeft, "PushBadge 应位于 PullBadge 右侧");

						ScreenshotHelper.Snap(window, "03-toolbar-ahead-behind-badges", "06-toolbar");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
