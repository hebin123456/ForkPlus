// E2E 模块2（2026-09-05）：提交历史视图（修订列表）。
// 覆盖：真实 git 数据加载（RefreshRepositoryData 管线）、行点击选中、搜索过滤、
// 搜索面板展开/关闭、列表方向切换（横向/纵向布局）。
// 截图 → docs/evidence/e2e/02-revisionlist/。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e02RevisionListTests
	{
		[Fact]
		public void RevisionList_LoadsSelectsSearchesAndSwitchesOrientation()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				var module = new GitModule(repo, System.IO.Path.Combine(repo, ".git"), null, null);
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new RepositoryUserControl();
					control.OpenRepository(module);
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// ===== 1) 初始加载（真实管线：JobQueue → RefreshRepositoryDataGitCommand → UpdateRepositoryData） =====
					control.InvalidateAndRefresh(SubDomain.All);
					var revList = control.Content.RevisionListViewUserControl;
					bool loaded = UiClick.WaitFor(delegate
					{
						return revList.RevisionsDataSource.Count > 0;
					});
					Assert.True(loaded, "修订列表未加载出数据（15s 超时）");
					int initialCount = revList.RevisionsDataSource.Count;
					// CreateBranches 有 6 个提交（含两个 feature 分支的提交）
					Assert.True(initialCount >= 5, "提交数应 >= 5，实际 " + initialCount);
					ScreenshotHelper.Snap(window, "01-revision-list-loaded", "02-revisionlist");

					// ===== 2) 行选中（生产启动同款路径：NoUIAutomationListView.Select；
					//      DragAndDropListView 定制指针逻辑会吞掉合成 PointerPressed，不适用于 headless） =====
					var listView = UiClick.Find<DragAndDropListView>(revList, "RevisionListView");
					var rows = UiClick.FindAll<ListBoxItem>(listView);
					Assert.True(rows.Count >= 3, "可见行数应 >= 3（虚拟化下的视口行），实际 " + rows.Count);
					listView.Select(0, NoUIAutomationListView.SelectOptions.ScrollIntoView);
					Dispatcher.UIThread.RunJobs();
					Assert.NotNull(revList.SelectedRevision);
					ScreenshotHelper.Snap(window, "02-revision-row-selected", "02-revisionlist");

					// ===== 3) 上下文搜索（标记匹配 + 跳转，不缩小行集——与产品语义一致） =====
					var searchPanel = UiClick.Find<RevisionSearchPanelUserControl>(revList, "RevisionSearchPanelUserControl");
					searchPanel.ShowSearchBar();
					Dispatcher.UIThread.RunJobs();
					var searchBox = UiClick.Find<ForkPlus.UI.Controls.PlaceholderTextBox>(searchPanel, "SearchTextBox");
					searchBox.Text = "feature";
					bool searched = UiClick.WaitFor(delegate
					{
						return revList.RevisionsDataSource.ContextSearchCount > 0;
					});
					Assert.True(searched, "上下文搜索应标记到匹配（ContextSearchCount>0）");
					int matchCount = revList.RevisionsDataSource.ContextSearchCount.Value;
					// "feature" 匹配：c4/c5 两条提交消息 + feature/one、feature/two 两个分支 ref
					Assert.True(matchCount >= 2, "匹配数应 >= 2，实际 " + matchCount);
					Assert.Equal(initialCount, revList.RevisionsDataSource.Count); // 行集不缩小
					Assert.True(searchPanel.MatchesTextBlock.Text.Contains("match"), "匹配计数文本应更新: " + searchPanel.MatchesTextBlock.Text);
					ScreenshotHelper.Snap(window, "03-revision-search-matches", "02-revisionlist");
					// 搜索跳转后选中行应为匹配行
					Assert.NotNull(revList.SelectedRevision);

					// ===== 4) 清空搜索恢复（匹配标记清除） =====
					searchBox.Text = "";
					bool restored = UiClick.WaitFor(delegate
					{
						return revList.RevisionsDataSource.ContextSearchCount == null
							|| revList.RevisionsDataSource.ContextSearchCount == 0;
					});
					Assert.True(restored, "清空搜索应清除匹配标记");
					searchPanel.HideSearchBar();
					Dispatcher.UIThread.RunJobs();
					ScreenshotHelper.Snap(window, "04-revision-search-cleared", "02-revisionlist");

					// ===== 5) 方向切换（横向↔纵向布局；NotificationCenter 全局事件驱动） =====
					var orientationBefore = ForkPlus.Settings.ForkPlusSettings.Default.RevisionListOrientation;
					new SwitchRevisionListOrientationCommand().Execute();
					Dispatcher.UIThread.RunJobs();
					ScreenshotHelper.Snap(window, "05-revision-orientation-switched", "02-revisionlist");
					// 还原设置（再切一次 + 直接写回）
					new SwitchRevisionListOrientationCommand().Execute();
					ForkPlus.Settings.ForkPlusSettings.Default.RevisionListOrientation = orientationBefore;
					Dispatcher.UIThread.RunJobs();
					window.Close();
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
