// WS2.3（重置分支丢失预览）测试：ResetBranchWindow 内容区的 CommitsPreviewSection。
// CreateHistoryRewrite 仓库（main = "base one" + "base two"）：
// - 重置 main → base one（main~1）：预览可见，摘要 "重置将丢失 1 个提交"，列表恰一条
//   "base two"（<目标>..<分支尖> 范围内分支侧独有提交），Expander 默认收起；
// - 目标 = 当前分支尖（main 自身）：0 条丢失 → 预览整块隐藏（静默降级路径）。
using System;
using System.Linq;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ResetBranchLoseCommitsPreviewTests
	{
		/// <summary>按 sha 取 Revision（与 E2e13 同款：生产命令 GetRevisionsGitCommand）。</summary>
		private static Revision RevisionFor(GitModule gitModule, string sha)
		{
			Assert.True(Sha.TryParse(sha, out Sha parsed), "sha 应可解析: " + sha);
			GitCommandResult<Revision[]> result = new GetRevisionsGitCommand().Execute(gitModule, new Sha[] { parsed });
			Assert.True(result.Succeeded, "GetRevisionsGitCommand 应成功: " + (result.Error?.FriendlyDescription ?? ""));
			Assert.Single(result.Result);
			return result.Result[0];
		}

		[Fact]
		public void ResetToOlderCommit_ShowsLoseCommitsPreview()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryData != null
								&& repoControl.RepositoryData.References.ActiveBranch != null;
						}), "引用/活跃分支应装配（15s 超时）");
						LocalBranch main = repoControl.RepositoryData.References.ActiveBranch;
						Assert.Equal("main", main.Name);
						string baseOneSha = TestRepoFactory.GitOutput(repo, "rev-parse main~1").Trim();
						Revision baseOne = RevisionFor(repoControl.GitModule, baseOneSha);

						ResetBranchWindow dialog = new ResetBranchWindow(repoControl, main, baseOne);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						try
						{
							// 丢失预览：main 重置到 main~1 将丢失 "base two" 这 1 个提交
							Assert.True(dialog.LoseCommitsPreviewSection.IsVisible, "存在丢失提交时预览应可见");
							Assert.Equal(E2eMainWindowHarness.TrFormat("Resetting will lose {0} commits", 1),
								dialog.LoseCommitsPreviewSection.SummaryTextBlock.Text);
							Assert.False(dialog.LoseCommitsPreviewSection.CommitsExpander.IsExpanded, "Expander 应默认收起");
							Assert.Equal(1, dialog.LoseCommitsPreviewSection.CommitsItemsControl.ItemCount);
							Assert.Equal("base two", ((CommitsPreviewSection.Item)dialog.LoseCommitsPreviewSection
								.CommitsItemsControl.ItemsSource.OfType<object>().First()).Subject);
						}
						finally
						{
							dialog.Close();
							Dispatcher.UIThread.RunJobs();
						}
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
		public void ResetToBranchTip_HidesLoseCommitsPreview()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryData != null
								&& repoControl.RepositoryData.References.ActiveBranch != null;
						}), "引用/活跃分支应装配（15s 超时）");
						LocalBranch main = repoControl.RepositoryData.References.ActiveBranch;
						// 目标 = 当前分支尖：无丢失提交 → 预览隐藏
						Revision tip = RevisionFor(repoControl.GitModule, TestRepoFactory.GitOutput(repo, "rev-parse main").Trim());
						ResetBranchWindow dialog = new ResetBranchWindow(repoControl, main, tip);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						try
						{
							Assert.False(dialog.LoseCommitsPreviewSection.IsVisible, "无丢失提交时预览应隐藏");
						}
						finally
						{
							dialog.Close();
							Dispatcher.UIThread.RunJobs();
						}
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
