// E2E 模块11（2026-09-05）：分支操作 9 窗口（10 用例）。
// 覆盖：创建（校验三态 + checkout 开关命令预览 + 真实建支切支）/检出/重命名/删本地/删远程
// （单分支 GitPointView 视图 + 多分支 GitPoints 列表两模式）/跟踪远程/多分支推送/worktree 检出/
// Lean 分支流程（Start 建支切支 → Finish 合回 main）。
// 模式：E2eMainWindowHarness.OpenRepository（真实 MainWindow 生产入口）→ 生产构造器建弹窗 →
// 控件树交互（UiClick.Click/Toggle + 属性直设走生产 TextChanged 管线）→ WaitFor 弹窗关闭
// （JobQueue 后台命令完成 → Close）→ TestRepoFactory.GitOutput 真实 git 状态断言。
// 截图走 2026-09-06 口径（ScreenshotHelper.Snap 内置）：主窗口 1920×1080，弹窗按自然比例。
//
// 本地化口径（模块 10 "Choose {0}" 教训的延伸对照，首跑实证修正）：
// - CreateBranchWindow 重复名警告虽是全串拼接直传 SetStatus，但 Translate 内置
//   TranslatePattern 格式键模式匹配回退（WPF 原仓 PreferencesLocalization.cs 同款机制），
//   全串 "Branch 'x' already exists" 命中格式键 → zh-Hans 翻译输出，按 TrFormat 断言。
// - LeanBranchingStartWindow / RenameLocalBranchWindow / TrackRemoteBranchWindow 直接用
//   string.Format(Translate(fmt)) / FormatCurrent → 格式键命中字典，同样按 TrFormat 断言。
//
// Lean 分支流程前置（IsSubmitAllowed 同步校验，ForkPlus-wpf 原仓同款语义）：
//   Finish 需 localMain 有上游且 AreInSync（超前仅左不为零可接受——正是待合并的工作），
//   故用 CreateRemoteBranches 形态 + 测试内先 push main 对齐 origin/main。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e11BranchOperationsTests
	{
		private const string ModuleDir = "11-branchops";

		// ============================ 共享助手 ============================

		/// <summary>等引用与工作区状态装配完成（后台 git 读取经 Dispatcher 回 UI；
		/// RepositoryStatus 是各弹窗构造器的硬依赖，如 CreateBranchWindow 的 WorkingDirectoryIsDirty）。</summary>
		private static RepositoryReferences WaitForLoaded(RepositoryUserControl control, int minLocalBranches)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return control.RepositoryData != null
					&& control.RepositoryData.References.LocalBranches.Length >= minLocalBranches
					&& control.RepositoryStatus != null;
			}), "引用/工作区状态未装配（15s 超时）");
			return control.RepositoryData.References;
		}

		/// <summary>等远程分支装配完成（CreateRemoteBranches 形态：origin/main + origin/remote-only）。</summary>
		private static RemoteBranch WaitForRemoteBranch(RepositoryUserControl control, string name)
		{
			RemoteBranch found = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				found = control.RepositoryData?.References.RemoteBranches
					.FirstOrDefault(b => b.Name == name);
				return found != null;
			}), "远程分支 " + name + " 未装配（15s 超时）");
			return found;
		}

		private static ForkPlusDialogFooter FooterOf(ForkPlusDialogWindow dialog)
		{
			ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		/// <summary>命令预览文本（ForkPlusDialogWindow.AddCommandPreview 生成的 Consolas TextBlock，
		/// 无 x:Name，按"git " 前缀定位——迁移版预览面板为代码构造，无稳定名字）。</summary>
		private static string CommandPreviewOf(ForkPlusDialogWindow dialog)
		{
			return dialog.GetVisualDescendants().OfType<TextBlock>()
				.FirstOrDefault(t => t.Text != null && t.Text.StartsWith("git ", StringComparison.Ordinal))?.Text ?? "";
		}

		/// <summary>点提交并等弹窗关闭（JobQueue 后台命令完成 → Close(result)）。
		/// 注意：Footer.Submit 无 IsEnabled 守卫（生产靠按钮禁用拦截），调用前必须先断言启用态。</summary>
		private static void SubmitAndWaitClose(ForkPlusDialogWindow dialog, string what)
		{
			ForkPlusDialogFooter footer = FooterOf(dialog);
			UiClick.Click(footer.SubmitButton);
			Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
				what + "应在命令完成后关闭弹窗（15s 超时）");
			Dispatcher.UIThread.RunJobs();
		}

		private static string Head(string repo)
		{
			return TestRepoFactory.GitOutput(repo, "symbolic-ref --short HEAD").Trim();
		}

		private static bool BranchExists(string repo, string branch)
		{
			return TestRepoFactory.GitOutput(repo, "branch --list " + branch).Trim().Length > 0;
		}

		// ============================ 1) 创建分支 ============================

		[Fact]
		public void CreateBranch_ValidationStates_AndCreateWithCheckout()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 3);
						Assert.Equal("main", references.ActiveBranch.Name);

						var dialog = new CreateBranchWindow(repoControl, references, references.ActiveBranch);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 1) 空名 → 禁用（显式清空：ctor 可能预填 RecentNewBranchPrefix/UnfinishedBranchName）
						dialog.BranchNameTextBox.Text = string.Empty;
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "空分支名应禁用提交");
						ScreenshotHelper.Snap(dialog, "01-create-branch-initial", ModuleDir);

						// 2) 重复名 → 禁用 + 警告（全串拼接传入，但 Translate 有 TranslatePattern
						//    格式键模式匹配回退（WPF 原仓 PreferencesLocalization.cs 同款机制），
						//    全串 "Branch 'x' already exists" 命中格式键 "Branch '{0}' already exists"
						//    → zh-Hans 输出与 TrFormat 一致，两仓行为一致）
						dialog.BranchNameTextBox.Text = "feature/one";
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "重复分支名应禁用提交");
						Assert.Equal(E2eMainWindowHarness.TrFormat("Branch '{0}' already exists", "feature/one"),
							footer.StatusMessageTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "02-create-branch-duplicate-warning", ModuleDir);

						// 3) 非法名 → ReferenceNameValidator 警告 + 禁用。用 ':'（HasControlASCIICharacters 拦截）。
					//    注：空格虽被 git 本体（check-ref-format）拒绝，但两仓 ReferenceNameValidator
					//    规则集相同均不拦空格（WPF 原仓同款，原始行为非迁移回归，不修源码）——
					//    即原版允许输入含空格分支名直到 git 命令失败，测试按原版语义避开空格用例。
					dialog.BranchNameTextBox.Text = "bad:name";
					Dispatcher.UIThread.RunJobs();
					Assert.False(footer.SubmitButton.IsEnabled, "非法分支名应禁用提交");
					Assert.True(footer.StatusMessageTextBlock.IsVisible, "非法名应有可见警告状态");

						// 4) 合法新名 → 启用；checkout 开关切换命令预览（branch ↔ checkout -b）
						dialog.BranchNameTextBox.Text = "feature/three";
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "合法新分支名应启用提交");
						Assert.False(footer.StatusMessageTextBlock.IsVisible, "合法名应清除警告");

						UiClick.Toggle(dialog.CheckoutAfterCreateCheckBox, false);
						Dispatcher.UIThread.RunJobs();
						Assert.Contains("git branch feature/three", CommandPreviewOf(dialog));

						UiClick.Toggle(dialog.CheckoutAfterCreateCheckBox, true);
						Dispatcher.UIThread.RunJobs();
						Assert.Contains("git checkout -b feature/three", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "03-create-branch-command-preview", ModuleDir);

						// 5) 提交（checkout=on 提交态）→ 真实建支 + 切支
						SubmitAndWaitClose(dialog, "创建分支");
						Assert.True(BranchExists(repo, "feature/three"), "git 应存在 feature/three 分支");
						Assert.Equal("feature/three", Head(repo));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 2) 检出分支 ============================

		[Fact]
		public void CheckoutBranch_SwitchesActiveBranch()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 3);
						LocalBranch featureOne = references.LocalBranches.First(b => b.Name == "feature/one");

						var dialog = new CheckoutBranchWindow(repoControl, featureOne, null);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 默认 IsSubmitAllowed = !IsOperationInProgress（无额外校验）+ 命令预览
						Assert.True(footer.SubmitButton.IsEnabled, "检出弹窗提交应默认启用");
						Assert.Contains("git checkout feature/one", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "04-checkout-branch", ModuleDir);

						SubmitAndWaitClose(dialog, "检出分支");
						Assert.Equal("feature/one", Head(repo));
						Assert.True(File.Exists(Path.Combine(repo, "one.txt")),
							"检出后工作区应含 feature/one 独有文件 one.txt");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 3) 重命名本地分支 ============================

		[Fact]
		public void RenameLocalBranch_PrefillValidationAndRename()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 3);
						LocalBranch featureTwo = references.LocalBranches.First(b => b.Name == "feature/two");

						var dialog = new RenameLocalBranchWindow(repoControl.GitModule, references, featureTwo, null);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 1) 预填当前名（newName=null → localBranch.Name）+ 同名禁用（无变化）
						Assert.Equal("feature/two", dialog.BranchNameTextBox.Text);
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "同名（无变化）应禁用提交");

						// 2) 重复名 → 禁用（格式键命中字典，zh-Hans 本地化）
						dialog.BranchNameTextBox.Text = "main";
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "改为已存在分支名应禁用提交");
						Assert.Equal(E2eMainWindowHarness.TrFormat("Branch '{0}' already exists", "main"),
							footer.StatusMessageTextBlock.Text);

						// 3) 合法新名 → 启用 + 命令预览（-m 旧 新）
						dialog.BranchNameTextBox.Text = "feature/two-renamed";
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "合法新名应启用提交");
						Assert.Contains("git branch -m feature/two feature/two-renamed", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "05-rename-branch", ModuleDir);

						// 4) 提交 → 真实改名（旧名消失）
						SubmitAndWaitClose(dialog, "重命名分支");
						Assert.True(BranchExists(repo, "feature/two-renamed"), "应存在 feature/two-renamed");
						Assert.False(BranchExists(repo, "feature/two"), "旧名 feature/two 应消失");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 4) 删除本地分支 ============================

		[Fact]
		public void RemoveLocalBranch_DeletesNonActiveBranch()
		{
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 3);
						LocalBranch featureOne = references.LocalBranches.First(b => b.Name == "feature/one");
						Assert.False(featureOne.IsActive, "前置：feature/one 非活跃分支（活跃分支不可删）");

						var dialog = new RemoveLocalBranchWindow(repoControl, references,
							new LocalBranch[] { featureOne }, repoControl.RepositoryData.Remotes);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 单分支模式：命令预览（-D 强删非活跃分支；标题断言不可达——DialogTitle 为
						// protected，InternalsVisibleTo 不覆盖 protected 成员，标题证据见截图）
						Assert.True(footer.SubmitButton.IsEnabled, "删非活跃分支提交应启用");
						Assert.Contains("git branch -D feature/one", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "06-remove-local-branch", ModuleDir);

						SubmitAndWaitClose(dialog, "删除本地分支");
						Assert.False(BranchExists(repo, "feature/one"), "feature/one 应被删除");
						Assert.Equal("main", Head(repo));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 5) 删除远程分支 ============================

		[Fact]
		public void RemoveRemoteBranch_DeletesRemoteRef()
		{
			string repo = TestRepoFactory.CreateRemoteBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 1);
						RemoteBranch remoteOnly = WaitForRemoteBranch(repoControl, "origin/remote-only");

						var dialog = new RemoveRemoteBranchWindow(repoControl, new RemoteBranch[] { remoteOnly }, references);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 单分支模式：GitPointView 单视图装配（GitPoints 列表容器折叠、ItemsSource 不装配
						// ——构造器按数量分流的既定设计，非迁移回归）+ 命令预览（push --delete）
						Assert.Same(remoteOnly, dialog.GitPointView.Value);
						Assert.False(dialog.GitPointsContainer.IsVisible, "单分支模式应折叠列表容器");
						Assert.True(footer.SubmitButton.IsEnabled, "删除远程分支提交应启用");
						Assert.Contains("git push origin --delete refs/heads/remote-only", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "07-remove-remote-branch", ModuleDir);

						SubmitAndWaitClose(dialog, "删除远程分支");
						// ls-remote 直查 bare 远程（本地 refs/remotes 可能残留未 prune，不作依据）
						string heads = TestRepoFactory.GitOutput(repo, "ls-remote --heads origin");
						Assert.DoesNotContain("remote-only", heads);
						Assert.Contains("refs/heads/main", heads);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 5b) 删除多远程分支（列表模式） ============================

		[Fact]
		public void RemoveRemoteBranches_MultiMode_ListsAndDeletesBoth()
		{
			string repo = TestRepoFactory.CreateRemoteBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 1);
						RemoteBranch rbOne = WaitForRemoteBranch(repoControl, "origin/rb-one");
						RemoteBranch rbTwo = WaitForRemoteBranch(repoControl, "origin/rb-two");

						var dialog = new RemoveRemoteBranchWindow(repoControl, new RemoteBranch[] { rbOne, rbTwo }, references);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 多分支模式：GitPoints 列表装配 2 项（单视图折叠，与单分支模式互补）
						RemoteBranch[] listed = dialog.GitPoints.ItemsSource.OfType<RemoteBranch>().ToArray();
						Assert.Equal(2, listed.Length);
						Assert.Contains(listed, b => b.Name == "origin/rb-one");
						Assert.Contains(listed, b => b.Name == "origin/rb-two");
						Assert.False(dialog.GitPointView.IsVisible, "多分支模式应折叠单视图");
						Assert.True(footer.SubmitButton.IsEnabled, "删除多远程分支提交应启用");
						Assert.Contains("git push origin --delete refs/heads/rb-one", CommandPreviewOf(dialog));
						Assert.Contains("git push origin --delete refs/heads/rb-two", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "07b-remove-remote-branches-multi", ModuleDir);

						SubmitAndWaitClose(dialog, "删除多远程分支");
						// ls-remote 直查 bare 远程（本地 refs/remotes 可能残留未 prune，不作依据）
						string heads = TestRepoFactory.GitOutput(repo, "ls-remote --heads origin");
						Assert.DoesNotContain("rb-one", heads);
						Assert.DoesNotContain("rb-two", heads);
						Assert.Contains("refs/heads/main", heads);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 6) 跟踪远程分支 ============================

		[Fact]
		public void TrackRemoteBranch_CreatesAndChecksOutTrackingBranch()
		{
			string repo = TestRepoFactory.CreateRemoteBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 1);
						RemoteBranch remoteOnly = WaitForRemoteBranch(repoControl, "origin/remote-only");

						var dialog = new TrackRemoteBranchWindow(repoControl, references.LocalBranches, remoteOnly);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 预填 ShortName（remote-only）+ 命令预览（checkout -b 本地名 远程名）
						Assert.Equal("remote-only", dialog.LocalBranchNameTextBox.Text);
						Assert.True(footer.SubmitButton.IsEnabled, "合法跟踪名应启用提交");
						Assert.Contains("git checkout -b remote-only origin/remote-only", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "08-track-remote-branch", ModuleDir);

						// 提交 → CreateLocalAndTrackRemoteBranchGitCommand（checkout --track -b）：
						// 建本地分支 + 设上游 + 切换
						SubmitAndWaitClose(dialog, "跟踪远程分支");
						Assert.True(BranchExists(repo, "remote-only"), "应建本地分支 remote-only");
						Assert.Equal("remote-only", Head(repo));
						Assert.Equal("origin",
							TestRepoFactory.GitOutput(repo, "config --get branch.remote-only.remote").Trim());
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 7) 多分支推送 ============================

		[Fact]
		public void PushMultipleBranches_ListsAndPushesBranches()
		{
			string repo = TestRepoFactory.CreateRemoteBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 3);
						Remote origin = repoControl.RepositoryData.Remotes.Items.First(r => r.Name == "origin");
						LocalBranch one = references.LocalBranches.First(b => b.Name == "feature/one");
						LocalBranch two = references.LocalBranches.First(b => b.Name == "feature/two");

						var dialog = new PushMultipleBranchesWindow(repoControl, new LocalBranch[] { one, two }, origin);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 列表装配：两分支均为新上游（UpstreamName 含 origin/<分支>，无既有远程对应）
						PushMultipleBranchesWindow.PushBranchItem[] items =
							dialog.BranchesItemsControl.ItemsSource.OfType<PushMultipleBranchesWindow.PushBranchItem>().ToArray();
						Assert.Equal(2, items.Length);
						Assert.Contains(items, i => i.BranchName == "feature/one" && i.UpstreamName.Contains("origin/feature/one"));
						Assert.Contains(items, i => i.BranchName == "feature/two" && i.UpstreamName.Contains("origin/feature/two"));
						Assert.True(footer.SubmitButton.IsEnabled, "多分支推送提交应启用");
						ScreenshotHelper.Snap(dialog, "09-push-multiple-branches", ModuleDir);

						SubmitAndWaitClose(dialog, "多分支推送");
						// OnSubmit 是"入队即关"模式（与 AddUndoable 型弹窗"命令完成才 Close"不同）：
						// Close() 在 JobQueue 后台 push 完成前就执行，须轮询远程终态而非立即断言
						Assert.True(UiClick.WaitFor(delegate
						{
							string heads = TestRepoFactory.GitOutput(repo, "ls-remote --heads origin");
							return heads.Contains("refs/heads/feature/one") && heads.Contains("refs/heads/feature/two");
						}), "后台推送应在超时内把两分支送上远程（15s 超时）");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 8) worktree 检出 ============================

		[Fact]
		public void CheckoutBranchAsWorktree_CreatesWorktree()
		{
			string repo = TestRepoFactory.CreateBranches();
			string worktreeContainer = repo + "-worktrees"; // RefreshPath：<repo>-worktrees/<branch(/→-)>
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 3);
						LocalBranch featureOne = references.LocalBranches.First(b => b.Name == "feature/one");

						var dialog = new CheckoutBranchAsWorktreeWindow(repoControl, featureOne);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 默认路径 = 容器/<分支名(/→-)>，提交启用
						string worktreePath = dialog.PathTextBox.Text.Trim();
						Assert.False(string.IsNullOrEmpty(worktreePath), "应预填默认 worktree 路径");
						Assert.EndsWith("feature-one", worktreePath);
						Assert.True(footer.SubmitButton.IsEnabled, "合法 worktree 路径应启用提交");
						Assert.Contains("git worktree add", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "10-checkout-as-worktree", ModuleDir);

						// 提交 → AddWorktreeGitCommand（真实 git worktree add）+ 打开 worktree tab
						SubmitAndWaitClose(dialog, "worktree 检出");
						string worktrees = TestRepoFactory.GitOutput(repo, "worktree list");
						Assert.Contains("feature-one", worktrees);
						// OnSubmit 末尾经 MainWindow.Instance.TabManager.OpenRepository(worktreePath) 开了
						// 新 tab（生产路径）——收尾关闭，避免残留 tab 进会话
						window.TabManager.CloseTab(worktreePath);
						Dispatcher.UIThread.RunJobs();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(worktreeContainer);
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 9) Lean 分支流程（Start + Finish） ============================

		[Fact]
		public void LeanBranching_StartCreatesAndChecksOut_FinishMergesIntoMain()
		{
			string repo = TestRepoFactory.CreateRemoteBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 3);
						Branch mainBranch = references.LocalMain(repoControl.GitModule);
						Assert.NotNull(mainBranch);
						Assert.Equal("main", mainBranch.Name);

						// Lean Finish 的 IsSubmitAllowed 要求 main 与上游同步 → 先对齐 origin/main
						TestRepoFactory.GitOutput(repo, "push -q origin main");

						// ===== Start：建支 + 检出（固定 checkout=true，命令预览 checkout -b） =====
						var start = new LeanBranchingStartWindow(repoControl, mainBranch);
						start.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter startFooter = FooterOf(start);

						start.BranchNameTextBox.Text = string.Empty;
						Dispatcher.UIThread.RunJobs();
						Assert.False(startFooter.SubmitButton.IsEnabled, "空名应禁用提交");

						start.BranchNameTextBox.Text = "main"; // 重复名 → 格式键警告（zh-Hans）
						Dispatcher.UIThread.RunJobs();
						Assert.False(startFooter.SubmitButton.IsEnabled, "重复名应禁用提交");
						Assert.Equal(E2eMainWindowHarness.TrFormat("Branch '{0}' already exists", "main"),
							startFooter.StatusMessageTextBlock.Text);

						start.BranchNameTextBox.Text = "lean-feature";
						Dispatcher.UIThread.RunJobs();
						Assert.True(startFooter.SubmitButton.IsEnabled, "合法名应启用提交");
						Assert.Contains("git checkout -b lean-feature", CommandPreviewOf(start));
						ScreenshotHelper.Snap(start, "11-lean-branching-start", ModuleDir);

						SubmitAndWaitClose(start, "Lean 建支");
						Assert.Equal("lean-feature", Head(repo));
						Assert.True(BranchExists(repo, "lean-feature"));

						// 在 lean-feature 上提交一笔（供 Finish 合并；单提交 → fast-forward 路径）
						File.WriteAllText(Path.Combine(repo, "lean.txt"), "lean work\n");
						TestRepoFactory.GitOutput(repo, "add lean.txt");
						TestRepoFactory.GitOutput(repo, "commit -q -m lean-work");

						// 生产：ShowDialog 返回后 InvalidateAndRefresh；Finish 构造读 References.ActiveBranch
						repoControl.InvalidateAndRefresh(SubDomain.References | SubDomain.Head);
						Assert.True(UiClick.WaitFor(delegate
						{
							LocalBranch active = repoControl.RepositoryData?.References.ActiveBranch;
							return active != null && active.Name == "lean-feature";
						}), "刷新后活跃分支应为 lean-feature（15s 超时）");

						// ===== Finish：合回 main（校验 main↔上游同步 + feature 超前可合并） =====
						var finish = new LeanBranchingFinishWindow(repoControl);
						finish.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter finishFooter = FooterOf(finish);

						Assert.True(finishFooter.SubmitButton.IsEnabled,
							"main 已同步 + lean-feature 超前 1 提交 → Finish 应启用");
						ScreenshotHelper.Snap(finish, "12-lean-branching-finish", ModuleDir);

						SubmitAndWaitClose(finish, "Lean 完结合并");
						Assert.Equal("main", Head(repo));
						// main 已含 lean-feature 的提交（fast-forward）
						Assert.Contains("lean.txt",
							TestRepoFactory.GitOutput(repo, "show main --name-only --format="));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}
	}
}
