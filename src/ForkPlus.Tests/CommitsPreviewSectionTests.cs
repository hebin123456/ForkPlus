// v3.13.0（WS5/WS2.2）：GetCommitsBetweenGitCommand（命令层）+ CommitsPreviewSection
// （PullWindow 拉取预览 / PushWindow 强推覆盖预览）覆盖。
// 命令层：ahead 形态（CreateBareRemote：origin/main..main=1 条 "c2 ahead"、反向=0）
// 与 limit 截断（CreateTags：count=2 不截断、列表只取最新 1 条，新→旧排序）。
// UI 层（CreateRemoteBehind + fetch → behind 1）：PullWindow 预览可见性/文案/Expander
// 默认收起/条数；PushWindow force 开关往返（关=隐藏、开="将覆盖 1 个远程提交"）。
using System;
using System.IO;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CommitsPreviewSectionTests
	{
		// ============================ 命令层 ============================

		[Fact]
		public void GetCommitsBetween_CountsAndParsesBothDirections()
		{
			string work = TestRepoFactory.CreateBareRemote();
			try
			{
				GitModule module = new GitModule(work, Path.Combine(work, ".git"), null, null);
				GetCommitsBetweenGitCommand command = new GetCommitsBetweenGitCommand();
				// ahead：本地 main 领先 origin/main 1 条（"c2 ahead"）
				GitCommandResult<GetCommitsBetweenGitCommand.CommitsBetweenResult> ahead = command.Execute(module, "origin/main", "main");
				Assert.True(ahead.Succeeded, "origin/main..main 应成功: " + (ahead.Error?.FriendlyDescription ?? ""));
				Assert.Equal(1, ahead.Result.Count);
				GetCommitsBetweenGitCommand.CommitPreview commit = Assert.Single(ahead.Result.Commits);
				Assert.Equal("c2 ahead", commit.Subject);
				Assert.True(TestRepoFactory.GitOutput(work, "rev-parse main").Trim().StartsWith(commit.Sha),
					"短 SHA 应为全 SHA 前缀: " + commit.Sha);
				Assert.True(string.Equals(commit.FullSha, TestRepoFactory.GitOutput(work, "rev-parse main").Trim(), StringComparison.OrdinalIgnoreCase),
					"FullSha 应为 rev-parse 的完整 40 位 SHA: " + commit.FullSha);
				// 反向：远程无独有提交 → count 0 + 空列表
				GitCommandResult<GetCommitsBetweenGitCommand.CommitsBetweenResult> behind = command.Execute(module, "main", "origin/main");
				Assert.True(behind.Succeeded, "main..origin/main 应成功: " + (behind.Error?.FriendlyDescription ?? ""));
				Assert.Equal(0, behind.Result.Count);
				Assert.Empty(behind.Result.Commits);
			}
			finally
			{
				TestRepoFactory.Cleanup(work);
			}
		}

		[Fact]
		public void GetCommitsBetween_LimitTruncatesListButNotCount()
		{
			string tags = TestRepoFactory.CreateTags();
			try
			{
				GitModule module = new GitModule(tags, Path.Combine(tags, ".git"), null, null);
				GetCommitsBetweenGitCommand command = new GetCommitsBetweenGitCommand();
				// 不限：HEAD~2..HEAD = 2 条，新→旧
				GitCommandResult<GetCommitsBetweenGitCommand.CommitsBetweenResult> full = command.Execute(module, "HEAD~2", "HEAD");
				Assert.True(full.Succeeded, "HEAD~2..HEAD 应成功: " + (full.Error?.FriendlyDescription ?? ""));
				Assert.Equal(2, full.Result.Count);
				Assert.Equal(2, full.Result.Commits.Length);
				Assert.Equal("third commit", full.Result.Commits[0].Subject);
				Assert.Equal("second commit", full.Result.Commits[1].Subject);
				// limit 1：count 仍为 2（rev-list --count），列表只取最新 1 条
				GitCommandResult<GetCommitsBetweenGitCommand.CommitsBetweenResult> limited = command.Execute(module, "HEAD~2", "HEAD", 1);
				Assert.True(limited.Succeeded);
				Assert.Equal(2, limited.Result.Count);
				GetCommitsBetweenGitCommand.CommitPreview only = Assert.Single(limited.Result.Commits);
				Assert.Equal("third commit", only.Subject);
			}
			finally
			{
				TestRepoFactory.Cleanup(tags);
			}
		}

		// ============================ UI 层 ============================

		[Fact]
		public void PullWindow_ShowsPreviewOfIncomingCommits()
		{
			string work = TestRepoFactory.CreateRemoteBehind();
			try
			{
				TestRepoFactory.GitOutput(work, "fetch -q origin");
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						PullWindow dialog = new PullWindow(repoControl, null);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate { return dialog.PullPreviewSection.IsVisible; }, 45000),
							"拉取预览应可见（behind 1，45s 超时——共享环境线程池可能被真实仓库刷新积压）");
						Assert.Equal(E2eMainWindowHarness.TrFormat("Will pull {0} commits", 1),
							dialog.PullPreviewSection.SummaryTextBlock.Text);
						Assert.False(dialog.PullPreviewSection.CommitsExpander.IsExpanded, "Expander 应默认收起");
						Assert.Equal(1, dialog.PullPreviewSection.CommitsItemsControl.ItemCount);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(work);
			}
		}

		[Fact]
		public void PushWindow_ForcePushTogglesOverwritePreview()
		{
			string work = TestRepoFactory.CreateRemoteBehind();
			try
			{
				TestRepoFactory.GitOutput(work, "fetch -q origin");
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						PushWindow dialog = new PushWindow(repoControl);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						// force 关：即使远程领先也不显示覆盖预览
						Assert.False(dialog.ForcePushPreviewSection.IsVisible, "force 关闭时应隐藏覆盖预览");
						// force 开：远程领先 1 → 显示"将覆盖 1 个远程提交"
						UiClick.Toggle(dialog.ForcePushCheckBox, true);
						Assert.True(UiClick.WaitFor(delegate { return dialog.ForcePushPreviewSection.IsVisible; }, 45000),
							"force 打开且远程领先时应显示覆盖预览（45s 超时——共享环境线程池可能被真实仓库刷新积压）");
						Assert.Equal(E2eMainWindowHarness.TrFormat("Will overwrite {0} remote commits", 1),
							dialog.ForcePushPreviewSection.SummaryTextBlock.Text);
						Assert.False(dialog.ForcePushPreviewSection.CommitsExpander.IsExpanded, "Expander 应默认收起");
						Assert.Equal(1, dialog.ForcePushPreviewSection.CommitsItemsControl.ItemCount);
						// force 关回落：立即隐藏
						UiClick.Toggle(dialog.ForcePushCheckBox, false);
						Assert.False(dialog.ForcePushPreviewSection.IsVisible, "force 关闭后应隐藏覆盖预览");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(work);
			}
		}
	}
}
