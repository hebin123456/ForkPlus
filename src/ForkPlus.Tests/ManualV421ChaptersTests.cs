// 用户手册 v4.2.1 新特性插图（2026-09-30）：为 4.2.1 新增的两个界面补手册截图。
//   1) 仓库健康检查窗口（ch-20 查看器）：TestRepoFactory.CreateBranches + merge feature/one
//      → 真 MainWindow 打开仓库 → 直构 RepositoryHealthWindow（生产入口
//      ShowRepositoryHealthWindowCommand 只是一层 ShowDialog 包装）→ 等六区装配完成
//      → 落图 20-viewers/07-repository-health.png。
//   2) 丢弃更改专用确认窗口（ch-05 变更与提交）：DiscardChangesWindow 直构（子仓组 +
//      文件组两个分组；按钮标题口径同 DiscardChangedFilesCommand.CreateButtonTitle，
//      按去重路径数计）→ 落图 05-commit/08-discard-changes-confirm.png。
// 复用 ManualScreenshotHelper / HeadlessAppBootstrap / E2eMainWindowHarness / UiClick /
// TestRepoFactory 既有基建；截图落盘 docs/manual/screenshots/<模块目录>/。
// 注意（E2eMainWindowHarness 铁律）：主窗口收尾一律 CloseRepositoryTab，绝不 Close()——
// 否则 MainWindow 的 Closed → lifetime.Shutdown() 会把 headless App 关停，殃及后续截图用例。
using System;
using System.Linq;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ManualV421ChaptersTests
	{
		[Fact]
		public void Ch20_RepositoryHealthWindow()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				// feature/one 合进 main：已合并分支区有内容（批量删除 + 二次确认是本版重点）
				TestRepoFactory.GitOutput(repo, "merge feature/one");
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new RepositoryHealthWindow(repoControl);
						dialog.Show();
						// Loaded → ShowHealth → JobQueue 后台计算 → Post 回 UI 装配六区
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.GetVisualDescendants().OfType<RepositoryHealthUserControl>()
								.FirstOrDefault()?.ContentContainer.IsVisible == true;
						}, 45000), "健康仪表盘应完成装配（45s 超时——共享环境线程池可能被真实仓库刷新积压）");
						ManualScreenshotHelper.Snap(dialog, "07-repository-health", "20-viewers");
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
		public void Ch05_DiscardChangesConfirmWindow()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			HeadlessAppBootstrap.Run(delegate
			{
				// 展示口径：一个子模块 + 三个普通文件（其中一个为删除）——分组标题与路径列表同生产
				ChangedFile[] files = new ChangedFile[]
				{
					new ChangedFile("src/ForkPlus/UI/MainWindow.axaml.cs", StatusType.Modified, StatusType.Modified),
					new ChangedFile("src/ForkPlus/TabManager.cs", StatusType.Modified, StatusType.Modified),
					new ChangedFile("docs/manual/index.html", StatusType.Deleted, StatusType.Deleted)
				};
				Submodule[] submodules = new Submodule[] { new Submodule("submodules/third_party", isActive: true) };
				// 按钮标题：同 DiscardChangedFilesCommand.CreateButtonTitle（按去重路径数格式化）
				var dialog = new DiscardChangesWindow(files, submodules,
					E2eMainWindowHarness.TrFormat("Discard Changes in {0} Files", 4));
				dialog.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					ManualScreenshotHelper.Snap(dialog, "08-discard-changes-confirm", "05-commit");
				}
				finally
				{
					dialog.Close();
					Dispatcher.UIThread.RunJobs();
				}
			});
		}
	}
}