// E2E 模块16（2026-09-05）：子模块与 Worktree（5 窗口 + SubmoduleDiff 视图，6 用例）。
// 覆盖：AddSubmodule（空/半填/齐备三态 + 无 URL 与含 URL 双预览形态 + 真实 add：本地路径
// 克隆、.gitmodules URL 记录、sub 落地）/DeleteSubmodule（预览 deinit+rm 双命令 + 真实
// 删除："指针前进 + 工作区脏"双状态子模块被 -f 强制移除，目录消失 + 索引清空）/
// SubmoduleDiff 视图（Commit 视图选 sub → FileDiffControl 分发 SubmoduleDiffUserControl：
// 标题/1↑ ahead 角标/“1 uncommitted file”/修订列表装配/Update 按钮隐藏）/CreateWorktree
// （空名/既有分支/被 worktree 占用分支三态警告 + 合法新名启用 + 路径自动派生 + 预览 +
// 真实创建：worktree add -b 新分支 + 主窗口自动开新标签）/CheckoutBranchAsWorktree
// （占用分支禁用 + feature/two 真实检出：worktree add 既有分支 + 新标签）/DeleteWorktree
// （预览 + 真实 remove：目录移除、worktree list 不再列出）。
//
// ⚠️ 源码 bug 修复（本模块实证，2026-09-05）：git ≥ 2.38.1 出于安全默认禁止 file transport
// （protocol.file.allow=user），本地路径 URL 的子模块操作一律失败（“fatal: transport 'file'
// not allowed”，沙箱 git 2.50.1 探针实证）。AddSubmoduleWindow 明确支持本地路径（剪贴板识别
// 还专门检测本地目录），属迁移后新 git 环境下的功能性回归——已修复：
//   src/ForkPlus/Git/Commands/AddSubmoduleGitCommand.cs（submodule add 加 -c protocol.file.allow=always）
//   src/ForkPlus/Git/Commands/UpdateSubmodulesGitCommand.cs（submodule update 全部 4 处同因）
//
// 模式：真实 MainWindow 打开仓库（模块5 起口径）→ 生产构造器建弹窗（CreateWorktree/
// CheckoutBranchAsWorktree 依赖 RepositoryData 的 References/Worktrees，先 WaitFor 装配）→
// 控件树交互 → 提交等关（这些弹窗均为“命令完成才关”，与模块15 EditRemote 同类——直接
// SubmitAndWaitClose 即可断言 GitResult）→ 真实 git 终态复核。
// 截图走 2026-09-06 口径（ScreenshotHelper.Snap 内置）：主窗口 1920×1080，弹窗按自然比例。
//
// 仓库形态（TestRepoFactory）：CreateSubmoduleSource（独立源仓，供 add 克隆）/
// CreateSubmodule（parent + 前进脏子模块，供 delete/diff）/CreateWithWorktree（parent +
// feature/one 已检出为链接 worktree，供占用校验/删除/检出）。CreateSubmodule/CreateWithWorktree
// 返回 parent，root 下还有兄弟目录（subsrc/worktrees）——finally 清理整个 root 而非仅 parent。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e16SubmoduleWorktreeTests
	{
		private const string ModuleDir = "16-submodule-worktree";

		// ============================ 共享助手 ============================

		private static string GitOf(string repo, string args)
		{
			return TestRepoFactory.GitOutput(repo, args).Trim();
		}

		/// <summary>多目录仓库形态（CreateSubmodule/CreateWithWorktree）的 root：
		/// 返回的是 parent，兄弟目录（subsrc/worktrees 容器）同在 root 下，清理须删整个 root。</summary>
		private static string RootOf(string repo)
		{
			return Directory.GetParent(repo).FullName;
		}

		private static ForkPlusDialogFooter FooterOf(ForkPlusDialogWindow dialog)
		{
			ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		/// <summary>命令预览文本（ForkPlusDialogWindow.AddCommandPreview 生成的 Consolas TextBlock）。</summary>
		private static string CommandPreviewOf(ForkPlusDialogWindow dialog)
		{
			return dialog.GetVisualDescendants().OfType<TextBlock>()
				.FirstOrDefault(t => t.Text != null && t.Text.StartsWith("git ", StringComparison.Ordinal))?.Text ?? "";
		}

		/// <summary>点提交并等弹窗关闭。本模块弹窗均为“命令完成才关”（JobQueue 完成后
		/// Close(result)），关窗即命令终态——GitResult 直接可断言（模块15 EditRemote 同款）。</summary>
		private static void SubmitAndWaitClose(ForkPlusDialogWindow dialog, string what)
		{
			ForkPlusDialogFooter footer = FooterOf(dialog);
			UiClick.Click(footer.SubmitButton);
			Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
				what + "应在提交后关闭弹窗（15s 超时）");
			Dispatcher.UIThread.RunJobs();
		}

		// ============================ 1) AddSubmodule ============================

		[Fact]
		public void AddSubmodule_ValidationPreviewAndRealAdd()
		{
			string work = TestRepoFactory.CreateBasic();
			string subsrc = TestRepoFactory.CreateSubmoduleSource();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						// 显式传空 SubmodulesToUpdate（无嵌套子模块可拉取）
						var dialog = new AddSubmoduleWindow(repoControl.GitModule,
							new SubmodulesToUpdate(new Tuple<Submodule, bool>[0]));
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 1) 双空 → 禁用（显式置空覆盖构造器的剪贴板预填，保证确定性初态）
						dialog.RepositoryUrlTextBox.Text = string.Empty;
						dialog.PathTextBox.Text = string.Empty;
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "URL 与路径均空应禁用提交");

						// 2) 仅路径 → 仍禁用（IsSubmitAllowed 要求两者齐备）+ 无 URL 预览形态
						dialog.PathTextBox.Text = "sub";
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "URL 空时应禁用提交");
						Assert.Equal("git submodule add sub", CommandPreviewOf(dialog));

						// 3) URL + 路径 → 启用 + 完整预览（本地路径无空格不加引号）+ 绝对路径提示
						dialog.RepositoryUrlTextBox.Text = subsrc;
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "URL 与路径齐备应启用提交");
						Assert.Equal("git submodule add " + subsrc + " sub", CommandPreviewOf(dialog));
						Assert.True(dialog.FinalPathHintTextBlock.IsVisible, "输入路径后应显示最终路径提示");
						Assert.Contains("sub", dialog.FinalPathHintTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "01-addsubmodule-ready", ModuleDir);

						// 4) 真实提交：submodule add 克隆 subsrc → sub（file transport 修复后应成功）
						SubmitAndWaitClose(dialog, "添加子模块");
						Assert.True(dialog.GitResult != null && dialog.GitResult.Succeeded,
							"子模块添加命令应成功（file transport 修复验证）");

						// 真实仓库断言：.gitmodules 记录 URL、sub 内容落地（克隆默认检出 main@s2）、状态可查询
						Assert.True(UiClick.WaitFor(delegate
						{
							return File.Exists(Path.Combine(work, ".gitmodules"))
								&& File.Exists(Path.Combine(work, "sub", "s.txt"));
						}), "submodule add 应克隆 subsrc 到 sub 并落地 s.txt（15s 超时）");
						Assert.Equal(subsrc, GitOf(work, "config --file .gitmodules submodule.sub.url"));
						Assert.Contains(" sub", GitOf(work, "submodule status"));
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
				TestRepoFactory.Cleanup(subsrc);
			}
		}

		// ============================ 2) DeleteSubmodule ============================

		[Fact]
		public void DeleteSubmodule_PreviewAndRealDelete()
		{
			string repo = TestRepoFactory.CreateSubmodule();
			string root = RootOf(repo);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 等待子模块装配（.gitmodules → RepositoryData.Submodules）
						Submodule sub = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							sub = repoControl.RepositoryData?.Submodules?.Items.FirstOrDefault(s => s.Path == "sub");
							return sub != null;
						}), "子模块列表应装配 sub（15s 超时）");

						var dialog = new DeleteSubmoduleWindow(repoControl.GitModule, sub);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 删除确认弹窗：默认可提交 + deinit/rm 双命令预览（本地路径无引号）
						Assert.True(footer.SubmitButton.IsEnabled, "删除确认弹窗提交应默认启用");
						Assert.Equal("git submodule deinit -f sub && git rm -f sub", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "02-deletesubmodule", ModuleDir);

						// 真实提交：子模块处于"指针前进(s1→s2) + 工作区脏(s.txt)"双状态，
						// deinit -f 强制反注册 + rm -r 强制移除（命令完成才关 → 关窗即终态）
						SubmitAndWaitClose(dialog, "删除子模块");
						Assert.True(dialog.GitResult != null && dialog.GitResult.Succeeded,
							"脏子模块删除命令应成功（-f 强制）");

						// 真实仓库断言：sub 目录消失 + 索引中 gitlink 清空
						Assert.True(UiClick.WaitFor(delegate
						{
							return !Directory.Exists(Path.Combine(repo, "sub"));
						}), "sub 目录应被移除（15s 超时）");
						Assert.Equal(string.Empty, GitOf(repo, "ls-files -- sub"));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		// ============================ 3) SubmoduleDiff 视图 ============================

		[Fact]
		public void SubmoduleDiffView_ShowsAheadAndDirtyState()
		{
			string repo = TestRepoFactory.CreateSubmodule();
			string root = RootOf(repo);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 切到 Commit 视图（生产公共入口），等工作区状态装配：
						// sub 是唯一未暂存变更（指针前进 s1→s2 + 内部 s.txt 脏）
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 1 && stage.AllUnstagedFiles[0].Path == "sub";
						}), "工作区状态应装配唯一的未暂存变更 sub（15s 超时）");
						Assert.Equal(ChangeType.Modified, stage.AllUnstagedFiles[0].ChangeType);

						// 选中 sub → FileDiffControl 识别 SubmoduleChangedFile → 异步装配
						// SubmoduleDiffUserControl（GetSubmoduleDiffContentGitCommand 全链路）
						stage.UnstagedFilesFileListUserControl.SelectFile("sub");
						Dispatcher.UIThread.RunJobs();
						SubmoduleDiffUserControl subDiff = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							subDiff = UiClick.FindAll<SubmoduleDiffUserControl>(window).FirstOrDefault();
							return subDiff != null;
						}), "选中 sub 后应出现子模块 diff 视图（15s 超时）");

						// 标题 / ahead 角标 / 未提交文件数（父仓记录 s1，子模块 HEAD s2 → 1↑；
						// 子模块工作区 s.txt 脏 → 1 uncommitted file）
						Assert.Equal(E2eMainWindowHarness.TrFormat("Submodule '{0}' changed", "sub"),
							subDiff.TitleTextBlock.Text);
						Assert.Equal("1↑", subDiff.BehindAheadTextBlock.Text);
						Assert.Equal(E2eMainWindowHarness.TrFormat("{0} uncommitted file", 1),
							subDiff.UncommittedFilesTextBlock.Text);

						// 修订列表：s1..s2 区间的子模块新提交（s2 "sub v2"）装配进 RevisionListView
						Assert.True(UiClick.WaitFor(delegate
						{
							return subDiff.RevisionListView.ItemCount >= 1;
						}), "子模块修订列表应装配（15s 超时）");

						// Update 按钮仅 Commit 视图 + 子模块无未提交变更时显示——此处有脏文件应隐藏
						Assert.False(subDiff.UpdateSubmoduleButton.IsVisible,
							"有未提交变更时 Update 按钮应隐藏");
						ScreenshotHelper.Snap(window, "03-submodulediff", ModuleDir);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		// ============================ 4) CreateWorktree ============================

		[Fact]
		public void CreateWorktree_ValidationPreviewAndRealCreate()
		{
			string repo = TestRepoFactory.CreateWithWorktree();
			string root = RootOf(repo);
			string worktreesContainer = Path.Combine(root, "parent-worktrees");
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 等 RepositoryData 装配（分支 + worktrees：CreateWorktreeWindow 构造即读取）
						LocalBranch main = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							main = repoControl.RepositoryData?.References.LocalBranches.FirstOrDefault(b => b.Name == "main");
							return main != null
								&& repoControl.RepositoryData.Worktrees.Items.Any(w => w.FriendlyName == "wt-one");
						}), "分支与 worktree 数据应装配（15s 超时）");

						var dialog = new CreateWorktreeWindow(repoControl, main);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 1) 初始（分支名空）→ 禁用（构造器 UpdateSubmitButton 已评估）
						Assert.False(footer.SubmitButton.IsEnabled, "分支名空应禁用提交");
						Assert.Equal("main", ((LocalBranch)dialog.LocalBranchesComboBox.SelectedItem).Name);

						// 2) 既有分支名 → 警告 + 禁用（SetStatus 走本地化模板匹配 → 用 TrFormat 断言）
						dialog.BranchNameTextBox.Text = "feature/two";
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "既有分支名应禁用提交");
						Assert.Equal(E2eMainWindowHarness.TrFormat("Branch '{0}' already exists", "feature/two"),
							footer.StatusMessageTextBlock.Text);

						// 3) 已被 worktree 占用的分支名 → 另一种警告 + 禁用
						//   （feature/one 已检出为 wt-one：WorktreesByFullReference 拦截）
						dialog.BranchNameTextBox.Text = "feature/one";
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "被 worktree 占用的分支名应禁用提交");
						Assert.Equal(E2eMainWindowHarness.TrFormat("Worktree '{0}' already exists", "feature/one"),
							footer.StatusMessageTextBlock.Text);

						// 4) 合法新分支名 → 启用 + 警告清除 + 路径自动派生（容器/分支名）+ 预览
						dialog.BranchNameTextBox.Text = "wtbranch";
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "合法新分支名应启用提交");
						Assert.False(footer.StatusMessageTextBlock.IsVisible, "合法名应清除警告");
						string expectedPath = Path.Combine(worktreesContainer, "wtbranch");
						Assert.Equal(expectedPath, dialog.PathTextBox.Text);
						Assert.Equal("git worktree add " + expectedPath + " wtbranch", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "04-createworktree", ModuleDir);

						// 5) 真实提交：worktree add -b wtbranch <path> <main.sha> + 主窗口自动开新标签
						SubmitAndWaitClose(dialog, "创建 worktree");
						Assert.True(dialog.GitResult != null && dialog.GitResult.Succeeded,
							"worktree 创建命令应成功");

						string wtPath = expectedPath;
						Assert.True(UiClick.WaitFor(delegate
						{
							return GitOf(repo, "branch --list wtbranch").Length > 0
								&& GitOf(repo, "worktree list --porcelain").Contains(wtPath)
								&& File.Exists(Path.Combine(wtPath, "main.txt"));
						}), "应创建 wtbranch 分支与链接 worktree 并检出内容（15s 超时）");

						// 新标签自动打开（OnSubmit → TabManager.OpenRepository(worktreePath)）
						Assert.True(UiClick.WaitFor(delegate
						{
							return window.TabManager.ActiveRepositoryUserControl != null
								&& window.TabManager.ActiveRepositoryUserControl.GitModule.Path == wtPath;
						}), "创建完成后应自动打开 worktree 标签（15s 超时）");

						// 收尾关闭两个标签（worktree 标签 + 父仓标签），防泄漏到后续用例
						E2eMainWindowHarness.CloseRepositoryTab(window, wtPath);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		// ============================ 5) CheckoutBranchAsWorktree ============================

		[Fact]
		public void CheckoutBranchAsWorktree_OccupiedDisabledAndRealCheckout()
		{
			string repo = TestRepoFactory.CreateWithWorktree();
			string root = RootOf(repo);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 等装配：feature/one（已占用）与 feature/two（空闲）
						LocalBranch featureOne = null;
						LocalBranch featureTwo = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							var branches = repoControl.RepositoryData?.References.LocalBranches;
							featureOne = branches?.FirstOrDefault(b => b.Name == "feature/one");
							featureTwo = branches?.FirstOrDefault(b => b.Name == "feature/two");
							return featureOne != null && featureTwo != null
								&& repoControl.RepositoryData.Worktrees.Items.Any(w => w.FriendlyName == "wt-one");
						}), "分支与 worktree 数据应装配（15s 超时）");

						// 1) 已占用分支（feature/one 已检出为 wt-one）→ IsSubmitAllowed 拦截 → 禁用
						//    （路径已预填，预览仍生成——反映将执行的命令形态）
						var occupied = new CheckoutBranchAsWorktreeWindow(repoControl, featureOne);
						occupied.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter occupiedFooter = FooterOf(occupied);
						Assert.False(occupiedFooter.SubmitButton.IsEnabled,
							"分支已被 worktree 占用应禁用提交");
						string occupiedPath = Path.Combine(root, "parent-worktrees", "feature-one");
						Assert.Equal(occupiedPath, occupied.PathTextBox.Text);
						Assert.Equal("git worktree add " + occupiedPath + " feature/one",
							CommandPreviewOf(occupied));
						occupied.Close();
						Dispatcher.UIThread.RunJobs();

						// 2) 空闲既有分支（feature/two）→ 启用 + 路径按分支名派生（/ → -）+ 预览
						var dialog = new CheckoutBranchAsWorktreeWindow(repoControl, featureTwo);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);
						Assert.True(footer.SubmitButton.IsEnabled, "空闲分支 + 路径预填应启用提交");
						string expectedPath = Path.Combine(root, "parent-worktrees", "feature-two");
						Assert.Equal(expectedPath, dialog.PathTextBox.Text);
						Assert.Equal("git worktree add " + expectedPath + " feature/two",
							CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "05-checkoutasworktree", ModuleDir);

						// 3) 真实提交：worktree add <path> feature/two（检出既有分支）+ 自动开新标签
						SubmitAndWaitClose(dialog, "检出分支为 worktree");
						Assert.True(dialog.GitResult != null && dialog.GitResult.Succeeded,
							"worktree 检出命令应成功");
						Assert.True(UiClick.WaitFor(delegate
						{
							return GitOf(repo, "worktree list --porcelain").Contains(expectedPath)
								&& File.Exists(Path.Combine(expectedPath, "main.txt"));
						}), "应创建 feature-two 链接 worktree 并检出内容（15s 超时）");
						Assert.True(UiClick.WaitFor(delegate
						{
							return window.TabManager.ActiveRepositoryUserControl != null
								&& window.TabManager.ActiveRepositoryUserControl.GitModule.Path == expectedPath;
						}), "检出完成后应自动打开 worktree 标签（15s 超时）");

						E2eMainWindowHarness.CloseRepositoryTab(window, expectedPath);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		// ============================ 6) DeleteWorktree ============================

		[Fact]
		public void DeleteWorktree_PreviewAndRealRemove()
		{
			string repo = TestRepoFactory.CreateWithWorktree();
			string root = RootOf(repo);
			string wtPath = Path.Combine(root, "parent-worktrees", "wt-one");
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 等装配：wt-one 链接 worktree（GetWorktreesGitCommand 读 .git/worktrees）
						Worktree? wt = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							wt = repoControl.RepositoryData?.Worktrees.Items
								.FirstOrDefault(w => w.FriendlyName == "wt-one");
							return wt.HasValue;
						}), "worktree 列表应装配 wt-one（15s 超时）");

						var dialog = new DeleteWorktreeWindow(repoControl, wt.Value);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 删除确认弹窗：默认可提交 + remove 命令预览（本地路径无引号）
						Assert.True(footer.SubmitButton.IsEnabled, "删除确认弹窗提交应默认启用");
						Assert.Equal("git worktree remove " + wtPath, CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "06-deleteworktree", ModuleDir);

						// 真实提交：worktree remove --force <path>（命令完成才关）
						SubmitAndWaitClose(dialog, "删除 worktree");
						Assert.True(dialog.GitResult != null && dialog.GitResult.Succeeded,
							"worktree 删除命令应成功");

						// 真实仓库断言：目录移除 + worktree list 不再列出
						Assert.True(UiClick.WaitFor(delegate
						{
							return !Directory.Exists(wtPath);
						}), "wt-one 目录应被移除（15s 超时）");
						Assert.DoesNotContain(wtPath, GitOf(repo, "worktree list"));
						// 分支本身保留（worktree remove 不删分支，仅解除占用）
						Assert.NotEmpty(GitOf(repo, "branch --list feature/one"));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}
	}
}
