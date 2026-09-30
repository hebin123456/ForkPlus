// WS4 仓库健康仪表盘（RepositoryHealthWindow）E2E：2 用例。
// 覆盖：窗口直构（生产入口 ShowRepositoryHealthWindowCommand 只是一层 ShowDialog 包装）→
// Loaded → RepositoryHealthUserControl.ShowHealth → JobQueue("RepositoryHealth") 后台计算
// （.git 目录体积枚举 + GetMergedBranchesGitCommand + UpstreamStatusCache + GetBiggestBlobsGitCommand）
// → Dispatcher.Post 回 UI 装配六区。
//
// 用例① 合并分支区内容（CreateBranches + git merge feature/one）：merged 区应含 feature/one、
//   不含 main（基准自身+活跃分支排除）与 feature/two（未合并）；陈旧区列出全部 3 个本地分支
//   按 CommitterDate 升序（工厂连发提交时间严格递增，merge 提交最后）；关注项计数 = merged 数。
// 用例② 空状态渲染（CreateClean：main 单提交无旁支）：merged/diverged 区走空状态文案，
//   陈旧区唯一分支 main，大文件区含 a.txt，概览条 .git 体积非空。
//
// 注：断言用结构性包含而非精确名次/计数——本机后台服务可能向临时仓库追加探测提交
// （口径同 GetBranchSafetyPreviewGitCommandTests / E2e20）。unborn-HEAD（零提交）的
// --merged=HEAD 失败降级路径不在窗口级覆盖：OpenRepository 对空仓库的 RepositoryData
// 装配行为未在其他模块实证，命令级空仓库行为由 GetBiggestBlobsGitCommandTests 覆盖。
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.VisualTree;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RepositoryHealthWindowTests
	{
		[Fact]
		public void RepositoryHealthWindow_MergedBranchesSection_ListsMergedBranchesOnly()
		{
			// 预置：把 feature/one 合进 main（one.txt 与 main.txt 无重叠 → 干净合并）
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				TestRepoFactory.GitOutput(repo, "merge feature/one");
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new RepositoryHealthWindow(repoControl);
						dialog.Show();
					// Loaded → ShowHealth → JobQueue 后台计算 → Post → ContentContainer.Show
					Assert.True(UiClick.WaitFor(delegate
					{
						return dialog.GetVisualDescendants().OfType<RepositoryHealthUserControl>()
							.FirstOrDefault()?.ContentContainer.IsVisible == true;
					}, 45000), "健康仪表盘应完成装配（45s 超时——共享环境线程池可能被真实仓库刷新积压）");
						RepositoryHealthUserControl health = dialog.GetVisualDescendants().OfType<RepositoryHealthUserControl>().First();

						// —— 合并分支区：feature/one 已合入 main（LeanBranchingMainBranch 未配置 → 基准 HEAD=main）——
						var merged = health.MergedBranchList.ItemsSource as ObservableCollection<RepositoryHealthUserControl.BranchHealthItemViewModel>;
						Assert.NotNull(merged);
						Assert.Contains(merged, (RepositoryHealthUserControl.BranchHealthItemViewModel x) => x.Name == "feature/one");
						// 基准自身（main）与未合并分支（feature/two）不得出现
						Assert.DoesNotContain(merged, (RepositoryHealthUserControl.BranchHealthItemViewModel x) => x.Name == "main");
						Assert.DoesNotContain(merged, (RepositoryHealthUserControl.BranchHealthItemViewModel x) => x.Name == "feature/two");
						Assert.True(health.MergedEmptyText.IsVisible == false, "有已合并分支时不应显示空状态");
						Assert.True(health.MergedDeleteButton.IsVisible == true, "有已合并分支时应显示删除按钮");

					// —— 区块 100% 宽（2026-09-30 修复，"各区块宽度太窄"）：内容容器由 StackPanel
					// 改 Grid 后，分区应横向铺满容器（修复前垂直 StackPanel 收缩到内容宽）。
					// ContentContainer.IsVisible 置位先于布局结算，宽度断言须 WaitFor 布局收敛。
					Assert.True(UiClick.WaitFor(delegate
					{
						return health.StaleBranchSection.Bounds.Width >= health.ContentContainer.Bounds.Width - 1.0;
					}), "陈旧分支分区应铺满内容容器宽度（实际 " + health.StaleBranchSection.Bounds.Width + " / 容器 " + health.ContentContainer.Bounds.Width + "）");

						// —— 陈旧分支区：全部 3 个本地分支按 CommitterDate 升序（新提交非陈旧）——
						var stale = health.StaleBranchList.ItemsSource as ObservableCollection<RepositoryHealthUserControl.BranchHealthItemViewModel>;
						Assert.NotNull(stale);
						Assert.True(stale.Count >= 3, "应至少列出工厂的 3 个本地分支");
						Assert.Equal("feature/one", stale[0].Name); // c4 最老（main 尖端是刚做的 merge 提交）
						Assert.True(stale.All((RepositoryHealthUserControl.BranchHealthItemViewModel x) => x.IsChecked == false), "默认不勾选");
						Assert.True(health.StaleEmptyText.IsVisible == false, "有分支时不应显示空状态");

						// —— 概览条：关注项 = 陈旧(0) + 已合并(merged.Count) + 分叉(0)——
						Assert.Equal(merged.Count.ToString(), health.AttentionItemsValueText.Text);
						Assert.True(!string.IsNullOrEmpty(health.GitSizeValueText.Text), ".git 体积应非空");

						// —— 分叉区：仓库无远程 → 空状态 ——
						Assert.True(health.DivergedEmptyText.IsVisible == true, "无 upstream 的仓库分叉区应显示空状态");

						// —— 大文件区：工厂提交的 blob 应出现 ——
						var blobs = health.LargestFilesList.ItemsSource as System.Collections.Generic.List<RepositoryHealthUserControl.BigBlobItemViewModel>;
						Assert.NotNull(blobs);
						Assert.Contains(blobs, (RepositoryHealthUserControl.BigBlobItemViewModel x) => x.Path == "main.txt");
						dialog.Close();
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
		public void RepositoryHealthWindow_CleanRepo_RendersSectionsWithEmptyStates()
		{
			// CreateClean：main 单提交、无旁支、无远程 —— merged（基准=活跃=main 被排除）与
			// diverged（无 upstream）均走空状态；stale 区唯一分支；largest 区含 a.txt。
			string repo = TestRepoFactory.CreateClean();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new RepositoryHealthWindow(repoControl);
						dialog.Show();
					Assert.True(UiClick.WaitFor(delegate
					{
						return dialog.GetVisualDescendants().OfType<RepositoryHealthUserControl>()
							.FirstOrDefault()?.ContentContainer.IsVisible == true;
					}, 45000), "健康仪表盘应完成装配（45s 超时——共享环境线程池可能被真实仓库刷新积压）");
						RepositoryHealthUserControl health = dialog.GetVisualDescendants().OfType<RepositoryHealthUserControl>().First();

						// —— 陈旧分支区：唯一分支 main（新提交，不标陈旧但照常列出）——
						var stale = health.StaleBranchList.ItemsSource as ObservableCollection<RepositoryHealthUserControl.BranchHealthItemViewModel>;
						Assert.NotNull(stale);
						Assert.True(stale.Count >= 1, "应列出 main 分支");
						Assert.Contains(stale, (RepositoryHealthUserControl.BranchHealthItemViewModel x) => x.Name == "main");
						Assert.True(health.StaleEmptyText.IsVisible == false, "有分支时不应显示空状态");
						Assert.True(health.StaleDeleteButton.IsVisible == true, "有分支时应显示删除按钮");

						// —— 合并分支区：main 既是基准又是活跃分支 → 空状态 ——
						var merged = health.MergedBranchList.ItemsSource as ObservableCollection<RepositoryHealthUserControl.BranchHealthItemViewModel>;
						Assert.NotNull(merged);
						Assert.Empty(merged);
						Assert.True(health.MergedEmptyText.IsVisible == true, "无已合并分支时应显示空状态");
						Assert.True(health.MergedDeleteButton.IsVisible == false, "无已合并分支时不应显示删除按钮");

						// —— 分叉分支区：无远程无 upstream → 空状态 ——
						Assert.True(health.DivergedEmptyText.IsVisible == true, "无分叉分支时应显示空状态");

						// —— 大文件区：a.txt blob 存在 ——
						var blobs = health.LargestFilesList.ItemsSource as System.Collections.Generic.List<RepositoryHealthUserControl.BigBlobItemViewModel>;
						Assert.NotNull(blobs);
						Assert.Contains(blobs, (RepositoryHealthUserControl.BigBlobItemViewModel x) => x.Path == "a.txt");

						// —— 概览条 + 体积明细：.git 体积非空、明细非空（hooks/info 等目录必存在）——
						Assert.True(!string.IsNullOrEmpty(health.GitSizeValueText.Text), ".git 体积应非空");
						var breakdown = health.SizeBreakdownList.ItemsSource as System.Collections.Generic.List<RepositoryHealthUserControl.GitDirSizeItemViewModel>;
						Assert.NotNull(breakdown);
						Assert.NotEmpty(breakdown);
						dialog.Close();
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
