// E2E 模块17（2026-09-06）：工作流套件——GitFlow（init + start/finish × 3）、GitMm、LeanBranching Sync。
//
// 覆盖：
//  1) GitFlowInit：预填（main 检测/develop/三前缀）+ 空值警告 + "git flow init" 预览 +
//     真实初始化（develop 分支 + gitflow config 全量写入 + flow init -d 保留 master=main）
//  2) GitFlowStartFeature：分支下拉装配（develop 选中）+ 前缀 TextBlock + 空名禁用 +
//     重复名警告（拼串直传非格式键）+ 预览 + 真实 start（feature/f2 建支切支）
//  3) GitFlowFinishFeature：下拉只列 feature/* + -r/--no-ff 选项预览组合 + 真实 finish
//     （-k 保留分支，f1 提交合回 develop，HEAD 切 develop）
//  4) GitFlowRelease：Start（预览+真实建支切支）→ 提交 → Finish（TagMessage + -k +
//     真实 finish：tag 1.0 创建 + rel 提交合回 main 与 develop）
//  5) GitFlowHotfix：Start（选 main 起点，预览含起点名）→ 提交 → Finish（真实 finish：
//     tag h1 + hfix 提交合回 main）
//  6) GitMmInit：三必填校验 + git mm init 预览（默认值/自定义值/空格引号）+
//     目标目录非空拦截（MessageBox 弹出文本断言，异步关闭避免模态死锁）
//  7) GitMm 检测与版本：IsGitMmWorkspace/.repo/.mm/FindAncestor + ParseVersion 多形态 +
//     Check 四态（假可执行脚本驱动 NotFound/Unsupported/Ok）+ 多行版本输出取首行
//  8) GitMm 工作区 tab：.mm 目录经 TabManager.OpenRepository 分流建 GitMm tab +
//     WorkspacePath/标题 + 无 git-mm CLI 时的 missing 警告弹窗（ExpectErrorDialogs 断言）
//  9) LeanBranching Sync：activeBranch≠main 且 main 与上游分叉（AreInSync 语义：纯落后/
//     纯超前均算同步，唯分叉拦截）→ "not in sync" 错误弹窗（校验路径）→ 回置纯落后并切回
//     main 后真实 Sync（状态机步进：Step1 rebase 快进 main 到 origin/main + .git/fork/sync
//     目录清理）
//
// ⚠️ 环境依赖（探针实证 2026-09-06）：GitFlow 真实链路依赖 gitflow-avh（沙箱纯净 git 无
// flow 子命令）——已源码安装到 ~/.local/bin 并写入 ~/.bashrc（跑测试须 PATH 含之；
// exec-path 方案无效：git 查找 git-flow 走 PATH 而非 exec-path）。命令链非交互性已探针：
// flow init -d 在 9 项 config 预置后不提问；feature/release/hotfix 的 start/finish
// （finish 带 -f <msgfile> -k）全部 exit=0。
//
// 模式：与模块16 相同——真实 MainWindow 打开仓库 → 生产构造器建弹窗（GitFlow Start/Finish
// 构造读 RepositoryData 的 GitFlowSettings/LocalBranches，先 WaitFor 装配）→ 控件树交互 →
// SubmitAndWaitClose（全部"命令完成才关"）→ 真实 git 终态复核。
// 新基建：HeadlessAppBootstrap.ExpectErrorDialogs/Take/Peek（模块17 起：预期错误弹窗
// ——Lean sync 校验与 GitMm missing 警告的弹窗本身就是被测行为，收尾不抛由测试断言文本）；
// E2eMainWindowHarness.WaitForRepositoryJobs（入队型命令真实执行后的等待）。
//
// GitMm 四命令窗口（Start/Reference/Sync/Upload）构造依赖 GitMmWorkspaceItem 真实子仓扫描
// （git mm scan CLI），沙箱无 git-mm CLI 不可测——记为环境不可测（变更日志说明），以
// Init 窗口 + 检测/版本逻辑 + 工作区 tab 装配覆盖 GitMm 的非 CLI 面。
//
// 截图走 2026-09-06 口径（ScreenshotHelper.Snap 内置）：主窗口 1920×1080，弹窗按自然比例。
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e17WorkflowTests
	{
		private const string ModuleDir = "17-workflows";

		// ============================ 共享助手（模块11/16 同款） ============================

		private static string GitOf(string repo, string args)
		{
			return TestRepoFactory.GitOutput(repo, args).Trim();
		}

		private static string Head(string repo)
		{
			return GitOf(repo, "symbolic-ref --short HEAD");
		}

		private static bool BranchExists(string repo, string branch)
		{
			return GitOf(repo, "branch --list " + branch).Length > 0;
		}

		private static ForkPlusDialogFooter FooterOf(ForkPlusDialogWindow dialog)
		{
			ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		private static string CommandPreviewOf(ForkPlusDialogWindow dialog)
		{
			return dialog.GetVisualDescendants().OfType<TextBlock>()
				.FirstOrDefault(t => t.Text != null && t.Text.StartsWith("git ", StringComparison.Ordinal))?.Text ?? "";
		}

		private static void SubmitAndWaitClose(ForkPlusDialogWindow dialog, string what)
		{
			ForkPlusDialogFooter footer = FooterOf(dialog);
			UiClick.Click(footer.SubmitButton);
			Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
				what + "应在提交后关闭弹窗（15s 超时）");
			Dispatcher.UIThread.RunJobs();
		}

		// 注意：GitFlowSettings 不在等待条件里——非 gitflow 仓库（未 init）合法为 null
		//（生产代码全量判 null：Toolbar/Sidebar/MainWindowMenuManager 等），需配置的用例
		// 另调 WaitForGitFlowSettings（模块17 首跑实证：CreateClean/CreateRemoteBehind 等 15s 超时）。
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

		/// <summary>等 GitFlowSettings 装配（仅 gitflow 仓库：GetGitFlowSettingsGitCommand
		/// 读 gitflow.branch.* config，未 init 的仓库返回 null）。</summary>
		private static void WaitForGitFlowSettings(RepositoryUserControl control)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return control.RepositoryData?.GitFlowSettings != null;
			}), "GitFlowSettings 未装配（15s 超时）");
		}

		/// <summary>等活跃分支变为指定名（真实 start/checkout 后刷新装配）。</summary>
		private static LocalBranch WaitForActiveBranch(RepositoryUserControl control, string name)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return control.RepositoryData?.References?.ActiveBranch?.Name == name;
			}), "活跃分支应变为 " + name + "（15s 超时）");
			return control.RepositoryData.References.ActiveBranch;
		}

		// ============================ 1) GitFlowInit ============================

		[Fact]
		public void GitFlowInit_ValidationPrefillAndRealInit()
		{
			string repo = TestRepoFactory.CreateClean();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForLoaded(repoControl, 1);
						var dialog = new GitFlowInitWindow(repoControl.GitModule);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 1) 预填：MainBranch() 检测 main 分支（CreateClean 用 -b main）+ 固定默认
						Assert.Equal("main", dialog.MasterBranchTextBox.Text);
						Assert.Equal("develop", dialog.DevelopBranchTextBox.Text);
						Assert.Equal("feature/", dialog.FeaturePrefixTextBox.Text);
						Assert.Equal("release/", dialog.ReleasePrefixTextBox.Text);
						Assert.Equal("hotfix/", dialog.HotfixPrefixTextBox.Text);
						Assert.True(footer.SubmitButton.IsEnabled, "预填齐备应启用提交");
						Assert.Equal("git flow init", CommandPreviewOf(dialog));

						// 2) 空 master → 禁用 + 警告（格式键经 TrFormat 断言本地化）
						dialog.MasterBranchTextBox.Text = string.Empty;
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "master 分支名空应禁用提交");
						Assert.Equal(E2eMainWindowHarness.TrFormat("Production branch name can't be empty"),
							footer.StatusMessageTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "01-gitflow-init-warning", ModuleDir);

						// 3) 恢复 + 真实初始化：develop 分支 + gitflow config 全量 + flow init -d 保留 master=main
						dialog.MasterBranchTextBox.Text = "main";
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "恢复后应启用提交");
						ScreenshotHelper.Snap(dialog, "02-gitflow-init-ready", ModuleDir);
						SubmitAndWaitClose(dialog, "Git Flow 初始化");
						Assert.True(dialog.GitResult != null && dialog.GitResult.Succeeded,
							"初始化命令应成功：" + (dialog.GitResult?.Error?.FriendlyDescription ?? "?"));

						Assert.True(BranchExists(repo, "develop"), "初始化应创建 develop 分支");
						Assert.Equal("main", GitOf(repo, "config --get gitflow.branch.master"));
						Assert.Equal("develop", GitOf(repo, "config --get gitflow.branch.develop"));
						Assert.Equal("feature/", GitOf(repo, "config --get gitflow.prefix.feature"));
						Assert.Equal("release/", GitOf(repo, "config --get gitflow.prefix.release"));
						Assert.Equal("hotfix/", GitOf(repo, "config --get gitflow.prefix.hotfix"));
						// flow init -d 保留预设的 master=main（而非 gitflow-avh 默认 master）
						Assert.Equal("main", GitOf(repo, "config --get gitflow.branch.master"));
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

		// ============================ 2) GitFlowStartFeature ============================

		[Fact]
		public void GitFlowStartFeature_ValidationPreviewAndRealStart()
		{
			string repo = TestRepoFactory.CreateGitFlow();
			try
			{
				// 供重复名校验的既有 feature 分支（命名冲突走 IsSubmitAllowed 拼串警告）
				TestRepoFactory.GitOutput(repo, "branch feature/f1");
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForLoaded(repoControl, 3); // main + develop + feature/f1
					WaitForGitFlowSettings(repoControl);
					var dialog = new GitFlowStartFeatureWindow(repoControl.GitModule);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 1) 装配：下拉含全部分支且 develop 选中（Refresh 逻辑 develop 优先）+ 前缀提示
						Assert.True(dialog.BranchesComboBox.SelectedItem is LocalBranch selected
							&& selected.Name == "develop", "默认应选中 develop 分支");
						Assert.Equal("feature/", dialog.FeaturePrefixTextBlock.Text);

						// 2) 空名 → 禁用
						Assert.False(footer.SubmitButton.IsEnabled, "空名应禁用提交");

						// 3) 重复名（feature/f1 已存在）→ 禁用 + 警告（拼串直传 SetStatus，但 Translate
					//    内置格式键回退命中 "Branch '{0}' already exists" → zh-Hans 输出，按 TrFormat 断言）
					dialog.FeatureNameTextBox.Text = "f1";
					Dispatcher.UIThread.RunJobs();
					Assert.False(footer.SubmitButton.IsEnabled, "重复名应禁用提交");
					Assert.Equal(E2eMainWindowHarness.TrFormat("Branch '{0}' already exists", "feature/f1"),
						footer.StatusMessageTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "03-gitflow-start-feature-dup", ModuleDir);

						// 4) 合法名 → 启用 + 完整预览（含起点分支名）
						dialog.FeatureNameTextBox.Text = "f2";
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "合法名应启用提交");
						Assert.Equal("git flow feature start f2 develop", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "04-gitflow-start-feature-ready", ModuleDir);

						// 5) 真实 start：feature/f2 建支 + 切换
						SubmitAndWaitClose(dialog, "Git Flow feature start");
						Assert.True(dialog.GitResult != null && dialog.GitResult.Succeeded,
							"feature start 命令应成功：" + (dialog.GitResult?.Error?.FriendlyDescription ?? "?"));
						Assert.True(BranchExists(repo, "feature/f2"), "应创建 feature/f2 分支");
						Assert.Equal("feature/f2", Head(repo));
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

		// ============================ 3) GitFlowFinishFeature ============================

		[Fact]
		public void GitFlowFinishFeature_OptionsPreviewAndRealFinish()
		{
			string repo = TestRepoFactory.CreateGitFlow();
			try
			{
				// 前置：feature/f1 建支切支 + 一笔提交（供 finish 合并）
				TestRepoFactory.GitOutput(repo, "flow feature start f1");
				File.WriteAllText(Path.Combine(repo, "f1.txt"), "feature work\n");
				TestRepoFactory.GitOutput(repo, "add f1.txt");
				TestRepoFactory.GitOutput(repo, "commit -q -m f1-work");

				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 3); // main + develop + feature/f1
						LocalBranch featureBranch = references.LocalBranches.FirstOrDefault(x => x.Name == "feature/f1");
						Assert.NotNull(featureBranch);
						var dialog = new GitFlowFinishFeatureWindow(repoControl.GitModule,
							repoControl.RepositoryData, featureBranch);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 1) 装配：下拉只列 feature/* 且当前 feature 选中
						Assert.True(dialog.BranchesComboBox.SelectedItem is LocalBranch sel && sel.Name == "feature/f1",
							"应选中传入的 feature 分支");
						var items = dialog.BranchesComboBox.ItemsSource.OfType<LocalBranch>().ToArray();
						Assert.True(items.Length == 1 && items[0].Name == "feature/f1",
							"下拉应只列 feature/* 分支（实际 " + items.Length + " 项）");

						// 2) 选项组合预览：-r 与 --no-ff 依序追加
						dialog.RebaseInsteadOfMergeCheckBox.IsChecked = false;
						dialog.NoFastForwardCheckBox.IsChecked = false;
						dialog.DeleteBranchesCheckBox.IsChecked = false;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git flow feature finish f1", CommandPreviewOf(dialog));
						dialog.RebaseInsteadOfMergeCheckBox.IsChecked = true;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git flow feature finish -r f1", CommandPreviewOf(dialog));
						dialog.NoFastForwardCheckBox.IsChecked = true;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git flow feature finish -r --no-ff f1", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "05-gitflow-finish-feature-ready", ModuleDir);

						// 3) 真实 finish（保持分支 -k；不 rebase/不 no-ff 以走普通合并路径）
						dialog.RebaseInsteadOfMergeCheckBox.IsChecked = false;
						dialog.NoFastForwardCheckBox.IsChecked = false;
						Dispatcher.UIThread.RunJobs();
						SubmitAndWaitClose(dialog, "Git Flow feature finish");
						Assert.True(dialog.GitResult != null && dialog.GitResult.Succeeded,
							"feature finish 命令应成功：" + (dialog.GitResult?.Error?.FriendlyDescription ?? "?"));
						Assert.Contains("f1.txt", GitOf(repo, "log develop --name-only --format="));
						Assert.True(BranchExists(repo, "feature/f1"), "不勾删除时应保留 feature/f1 分支");
						Assert.Equal("develop", Head(repo));
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

		// ============================ 4) GitFlowRelease：Start + Finish 真实链路 ============================

		[Fact]
		public void GitFlowRelease_StartAndFinishRealChain()
		{
			string repo = TestRepoFactory.CreateGitFlow();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForLoaded(repoControl, 2);
					WaitForGitFlowSettings(repoControl);
					// ===== Start Release =====
						var start = new GitFlowStartReleaseWindow(repoControl.GitModule);
						start.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter startFooter = FooterOf(start);
						Assert.False(startFooter.SubmitButton.IsEnabled, "空版本名应禁用提交");
						start.ReleaseNameTextBox.Text = "1.0";
						Dispatcher.UIThread.RunJobs();
						Assert.True(startFooter.SubmitButton.IsEnabled, "合法版本名应启用提交");
						Assert.Equal("git flow release start 1.0 develop", CommandPreviewOf(start));
						ScreenshotHelper.Snap(start, "06-gitflow-start-release-ready", ModuleDir);
						SubmitAndWaitClose(start, "Git Flow release start");
						Assert.True(start.GitResult != null && start.GitResult.Succeeded,
							"release start 命令应成功：" + (start.GitResult?.Error?.FriendlyDescription ?? "?"));
						Assert.True(BranchExists(repo, "release/1.0"), "应创建 release/1.0 分支");
						Assert.Equal("release/1.0", Head(repo));

						// ===== 在 release 分支提交一笔（供 finish 合并） =====
						File.WriteAllText(Path.Combine(repo, "rel.txt"), "release work\n");
						TestRepoFactory.GitOutput(repo, "add rel.txt");
						TestRepoFactory.GitOutput(repo, "commit -q -m rel-work");
						repoControl.InvalidateAndRefresh(SubDomain.References | SubDomain.Head);
						LocalBranch releaseBranch = WaitForActiveBranch(repoControl, "release/1.0");

						// ===== Finish Release（TagMessage + 保留分支） =====
						var finish = new GitFlowFinishReleaseWindow(repoControl.GitModule,
							repoControl.RepositoryData, releaseBranch);
						finish.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter finishFooter = FooterOf(finish);
						Assert.Equal("git flow release finish 1.0", CommandPreviewOf(finish));
						finish.TagMessageTextBox.Text = "release 1.0 tag";
						finish.DeleteBranchesCheckBox.IsChecked = false; // -k 保留 release/1.0
						Dispatcher.UIThread.RunJobs();
						ScreenshotHelper.Snap(finish, "07-gitflow-finish-release-ready", ModuleDir);
						SubmitAndWaitClose(finish, "Git Flow release finish");
						Assert.True(finish.GitResult != null && finish.GitResult.Succeeded,
							"release finish 命令应成功：" + (finish.GitResult?.Error?.FriendlyDescription ?? "?"));

						// 终态：tag 1.0 创建（versiontag 前缀空）+ rel 提交合回 main 与 develop
						// + HEAD 停在 develop（gitflow-avh release finish 链路：merge master → tag →
						//   checkout develop → merge，最后一步留在 develop——2026-09-06 沙箱实证）
						Assert.Contains("1.0", GitOf(repo, "tag --list 1.0"));
						Assert.Contains("rel.txt", GitOf(repo, "log main --name-only --format="));
						Assert.Contains("rel.txt", GitOf(repo, "log develop --name-only --format="));
						Assert.True(BranchExists(repo, "release/1.0"), "不勾删除时应保留 release/1.0 分支");
						Assert.Equal("develop", Head(repo));
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

		// ============================ 5) GitFlowHotfix：Start + Finish 真实链路 ============================

		[Fact]
		public void GitFlowHotfix_StartAndFinishRealChain()
		{
			string repo = TestRepoFactory.CreateGitFlow();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForLoaded(repoControl, 2);
					WaitForGitFlowSettings(repoControl);
					// ===== Start Hotfix（选 main 起点——hotfix 语义基于 master） =====
						var start = new GitFlowStartHotfixWindow(repoControl.GitModule);
						start.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter startFooter = FooterOf(start);
						LocalBranch mainBranch = references.LocalBranches.FirstOrDefault(x => x.Name == "main");
						Assert.NotNull(mainBranch);
						start.BranchesComboBox.SelectedItem = mainBranch;
						start.HotfixNameTextBox.Text = "h1";
						Dispatcher.UIThread.RunJobs();
						Assert.True(startFooter.SubmitButton.IsEnabled, "合法 hotfix 名应启用提交");
						Assert.Equal("git flow hotfix start h1 main", CommandPreviewOf(start));
						Assert.Equal("hotfix/", start.HotfixPrefixTextBlock.Text);
						ScreenshotHelper.Snap(start, "08-gitflow-start-hotfix-ready", ModuleDir);
						SubmitAndWaitClose(start, "Git Flow hotfix start");
						Assert.True(start.GitResult != null && start.GitResult.Succeeded,
							"hotfix start 命令应成功：" + (start.GitResult?.Error?.FriendlyDescription ?? "?"));
						Assert.True(BranchExists(repo, "hotfix/h1"), "应创建 hotfix/h1 分支");
						Assert.Equal("hotfix/h1", Head(repo));

						// ===== 在 hotfix 分支提交一笔 =====
						File.WriteAllText(Path.Combine(repo, "hfix.txt"), "hotfix work\n");
						TestRepoFactory.GitOutput(repo, "add hfix.txt");
						TestRepoFactory.GitOutput(repo, "commit -q -m hfix-work");
						repoControl.InvalidateAndRefresh(SubDomain.References | SubDomain.Head);
						LocalBranch hotfixBranch = WaitForActiveBranch(repoControl, "hotfix/h1");

						// ===== Finish Hotfix（TagMessage + 保留分支） =====
						var finish = new GitFlowFinishHotfixWindow(repoControl.GitModule,
							repoControl.RepositoryData, hotfixBranch);
						finish.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter finishFooter = FooterOf(finish);
						Assert.Equal("git flow hotfix finish h1", CommandPreviewOf(finish));
						finish.TagMessageTextBox.Text = "hotfix h1 tag";
						finish.DeleteBranchesCheckBox.IsChecked = false;
						Dispatcher.UIThread.RunJobs();
						ScreenshotHelper.Snap(finish, "09-gitflow-finish-hotfix-ready", ModuleDir);
						SubmitAndWaitClose(finish, "Git Flow hotfix finish");
						Assert.True(finish.GitResult != null && finish.GitResult.Succeeded,
							"hotfix finish 命令应成功：" + (finish.GitResult?.Error?.FriendlyDescription ?? "?"));

						// 终态：tag h1 + hfix 提交合回 main + HEAD 停在 develop
						//（gitflow-avh hotfix finish 链路：merge master → tag → checkout develop →
						//  merge，最后一步留在 develop——与 release finish 同款收尾，沙箱实证）
						Assert.Contains("h1", GitOf(repo, "tag --list h1"));
						Assert.Contains("hfix.txt", GitOf(repo, "log main --name-only --format="));
						Assert.Equal("develop", Head(repo));
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

		// ============================ 6) GitMmInit ============================
		// 注意：InitGitMmRepositoryWindow.RestoreDefaults 从 ForkPlusSettings.Default.GitMm
		// 恢复 manifest/branch/group 三字段（提交时 SaveDefaults 落盘持久化）——跨用例/跨轮次
		// 污染真实存在，故测试内显式清空三字段走 IsNullOrWhiteSpace 回退默认，不依赖持久值。

		[Fact]
		public void GitMmInit_ValidationPreviewAndDestinationGuard()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var dialog = new InitGitMmRepositoryWindow();
				dialog.Show();
				Dispatcher.UIThread.RunJobs();
				ForkPlusDialogFooter footer = FooterOf(dialog);

				// 1) 三必填：URL/父目录/仓库名（父目录预填 DefaultSourceDir，先清空造禁用态）
				dialog.ManifestUrlTextBox.Text = string.Empty;
				dialog.ParentDirectoryTextBox.Text = string.Empty;
				dialog.RepositoryNameTextBox.Text = string.Empty;
				Dispatcher.UIThread.RunJobs();
				Assert.False(footer.SubmitButton.IsEnabled, "三字段全空应禁用提交");

				// 2) 预览：manifest/branch/group 置空 → IsNullOrWhiteSpace 回退默认
				//    （dependency.xml/master/default；-g 恒出现在预览，CreateInitArgs 固定 9 参数）
				dialog.ManifestFileTextBox.Text = string.Empty;
				dialog.ManifestBranchTextBox.Text = string.Empty;
				dialog.ManifestGroupTextBox.Text = string.Empty;
				dialog.ManifestUrlTextBox.Text = "http://example.com/manifest.git";
				dialog.ParentDirectoryTextBox.Text = "/tmp/fpe2e_gitmm_dest";
				dialog.RepositoryNameTextBox.Text = "ws1";
				Dispatcher.UIThread.RunJobs();
				Assert.True(footer.SubmitButton.IsEnabled, "三字段齐备应启用提交");
				Assert.Equal("git mm init -u http://example.com/manifest.git -m dependency.xml -b master -g default",
					CommandPreviewOf(dialog));

				// 3) 自定义 manifest/branch/group + 空格 URL/group 引号转义
				dialog.ManifestFileTextBox.Text = "deps.xml";
				dialog.ManifestBranchTextBox.Text = "release";
				dialog.ManifestGroupTextBox.Text = "team a";
				dialog.ManifestUrlTextBox.Text = "http://example.com/my manifest.git";
				Dispatcher.UIThread.RunJobs();
				Assert.Equal("git mm init -u \"http://example.com/my manifest.git\" -m deps.xml -b release -g \"team a\"",
					CommandPreviewOf(dialog));

				// 3b) 复制按钮（2026-09-07，"命令预览右侧没有复制按钮"）：预览右侧存在且可见，
				//     点击后剪贴板内容与预览一致（Init 窗是 XAML 内联预览，不走基类 AddCommandPreview）
				Assert.NotNull(dialog.CommandPreviewCopyButton);
				Assert.True(dialog.CommandPreviewCopyButton.IsVisible, "命令预览右侧的复制按钮应可见");
				Assert.True(dialog.CommandPreviewCopyButton.Bounds.Left >= dialog.CommandPreviewTextBlock.Bounds.Right - 1,
					"复制按钮应位于命令预览文本右侧");
				UiClick.Click(dialog.CommandPreviewCopyButton);
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(CommandPreviewOf(dialog),
					global::ForkPlus.Services.ServiceLocator.Clipboard.GetText());

				ScreenshotHelper.Snap(dialog, "10-gitmm-init-ready", ModuleDir);

				// 4) 目标目录非空拦截：父目录指向已存在非空目录 → MessageBox 拦截
				//    关闭任务走 Dispatcher.Post（不能 closer.Wait 阻塞 UI 线程——InvokeAsync
				//    无人泵永不执行；Post 由下方 WaitFor 的 RunJobs 泵执行后弹窗才被关闭）
				string occupied = Path.Combine(Path.GetTempPath(), "fpe2e_gitmm_occupied");
				Directory.CreateDirectory(occupied);
				File.WriteAllText(Path.Combine(occupied, "seed.txt"), "occupied");
				dialog.ManifestUrlTextBox.Text = "http://example.com/manifest.git";
				dialog.ParentDirectoryTextBox.Text = occupied;
				dialog.RepositoryNameTextBox.Text = "ws1"; // occupied 本身非空 → destination=occupied 非空
				Dispatcher.UIThread.RunJobs();

				string messageBoxText = null;
				var closed = new System.Threading.ManualResetEventSlim(false);
				Task.Run(async delegate
				{
					await Task.Delay(500); // 等 MessageBox 弹出（OnSubmit 同步路径，ValidateDestination 直弹）
					Dispatcher.UIThread.Post(delegate
					{
						MessageBoxWindow box = (Avalonia.Application.Current?.ApplicationLifetime
							as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
							?.Windows.OfType<MessageBoxWindow>().FirstOrDefault(w => w.IsVisible);
						if (box != null)
						{
							messageBoxText = box.GetVisualDescendants().OfType<TextBlock>()
								.Select(t => t.Text).FirstOrDefault(t => t != null && t.Contains("already exists"))
								?? "<未找到 already exists 文本>";
							box.Close();
						}
						closed.Set();
					});
				});
				UiClick.Click(footer.SubmitButton); // ValidateDestination → MessageBox（未 await，弹后即返回）
				Assert.True(UiClick.WaitFor(delegate { return closed.IsSet; }),
					"MessageBox 关闭任务应执行（15s 超时）");
				Dispatcher.UIThread.RunJobs();
				Assert.NotNull(messageBoxText);
				Assert.Contains("already exists", messageBoxText); // 非空目录拦截文案
				ScreenshotHelper.Snap(dialog, "11-gitmm-init-destguard", ModuleDir);
				dialog.Close();
			});
		}

		// ============================ 6b) GitMm 四弹窗命令预览复制按钮（2026-09-07） ============================
		// "命令预览右侧没有复制按钮"：Init/Start/Sync/Upload 的预览区都是 XAML 内联实现（不走基类
		// AddCommandPreview，那条自带复制按钮），迁移时四个弹窗一并漏掉了。Start/Sync/Upload 依赖
		// 真实 workspace 子仓扫描才有完整流程（见类头"环境不可测"），这里直接以哑参数构造做
		// 构造级冒烟：按钮存在/可见/位于预览右侧，点击后剪贴板与预览一致。

		[Fact]
		public void GitMmDialogs_CommandPreviewCopyButton_Works()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ForkPlusDialogWindow[] dialogs =
				{
					new GitMmStartWindow(null, null),
					new GitMmSyncWindow("/tmp/fpe2e_gitmm_ws"),
					new GitMmUploadWindow("/tmp/fpe2e_gitmm_ws")
				};
				try
				{
					foreach (ForkPlusDialogWindow dialog in dialogs)
					{
						dialog.Show();
						Dispatcher.UIThread.RunJobs();

						TextBlock preview = dialog.GetVisualDescendants().OfType<TextBlock>()
							.FirstOrDefault(t => t.Name == "CommandPreviewTextBlock");
						Assert.NotNull(preview);
						Assert.False(string.IsNullOrWhiteSpace(preview.Text),
							dialog.GetType().Name + " 命令预览应有文本");

						Button copyButton = dialog.GetVisualDescendants().OfType<Button>()
							.FirstOrDefault(b => b.Name == "CommandPreviewCopyButton");
						Assert.NotNull(copyButton);
						Assert.True(copyButton.IsVisible, dialog.GetType().Name + " 复制按钮应可见");
						Assert.True(copyButton.Bounds.Left >= preview.Bounds.Right - 1,
							dialog.GetType().Name + " 复制按钮应位于命令预览右侧");

						UiClick.Click(copyButton);
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(preview.Text, global::ForkPlus.Services.ServiceLocator.Clipboard.GetText());
					}
				}
				finally
				{
					foreach (ForkPlusDialogWindow dialog in dialogs)
					{
						dialog.Close();
					}
				}
			});
		}

		// ============================ 7) GitMm 检测与版本逻辑（纯逻辑，无 UI） ============================

		[Fact]
		public void GitMm_DetectionAndVersionLogic()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpe2e_gitmm_logic_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string ws = Path.Combine(root, "ws");
			Directory.CreateDirectory(Path.Combine(ws, ".mm"));
			Directory.CreateDirectory(Path.Combine(root, "plain"));
			try
			{
				// IsGitMmWorkspace：.mm / .repo / 普通目录 / 空白
				Assert.True(GitMmUserControl.IsGitMmWorkspace(ws), ".mm 目录应识别为 git mm 工作区");
				Directory.CreateDirectory(Path.Combine(root, "repoWs", ".repo"));
				Assert.True(GitMmUserControl.IsGitMmWorkspace(Path.Combine(root, "repoWs")), ".repo 目录应识别为 git mm 工作区");
				Assert.False(GitMmUserControl.IsGitMmWorkspace(Path.Combine(root, "plain")), "普通目录不应识别");
				Assert.False(GitMmUserControl.IsGitMmWorkspace(""), "空串不应识别");
				Assert.False(GitMmUserControl.IsGitMmWorkspace(null), "null 不应识别");

				// FindAncestorGitMmWorkspace：子目录向上找最近工作区根（自身不算）
				string sub = Path.Combine(ws, "sub", "deep");
				Directory.CreateDirectory(sub);
				Assert.Equal(ws, GitMmUserControl.FindAncestorGitMmWorkspace(sub));

				// ParseVersion 多形态
				Assert.Equal(new Version(3, 0, 0), GitMmVersionChecker.ParseVersion("git-mm version 3.0.0"));
				Assert.Equal(new Version(3, 1, 2), GitMmVersionChecker.ParseVersion("git-mm 3.1.2"));
				Assert.Equal(new Version(2, 9, 1), GitMmVersionChecker.ParseVersion("2.9.1"));
				Assert.Equal(new Version(3, 0, 0), GitMmVersionChecker.ParseVersion("3.0"));
				Assert.Null(GitMmVersionChecker.ParseVersion("no digits here"));
				Assert.Null(GitMmVersionChecker.ParseVersion(""));

				// Check 四态：不存在 → NotFound
				Assert.Equal(GitMmVersionStatus.NotFound, GitMmVersionChecker.Check("/nonexistent/git-mm").Status);
				Assert.Equal(GitMmVersionStatus.NotFound, GitMmVersionChecker.Check(null).Status);
				Assert.Equal(GitMmVersionStatus.NotFound, GitMmVersionChecker.Check("").Status);

				// 假可执行脚本驱动 Unknown/Unsupported/Ok（Linux shell 脚本，chmod +x）
				string oldExe = MakeFakeGitMm(root, "git-mm version 2.0.1\nbuild info line2");
				GitMmVersionCheckResult old = GitMmVersionChecker.Check(oldExe);
				Assert.Equal(GitMmVersionStatus.Unsupported, old.Status);
				Assert.Equal(new Version(2, 0, 1), old.Version);

				string okExe = MakeFakeGitMm(root, "git-mm version 3.2.0\nbuild info line2");
				GitMmVersionCheckResult ok = GitMmVersionChecker.Check(okExe);
				Assert.Equal(GitMmVersionStatus.Ok, ok.Status);
				Assert.Equal(new Version(3, 2, 0), ok.Version);

				// 多行版本输出取首行（GetFirstLine：下拉框单行显示）
				string outExe = MakeFakeGitMm(root, "git-mm version 3.2.0\nsecond line");
				var cmdResult = new GetGitMmVersionShellCommand().Execute(outExe);
				Assert.True(cmdResult.Succeeded);
				Assert.Equal("git-mm version 3.2.0", cmdResult.Result); // 无内嵌换行

				// 无数字输出 → Unknown
				string junkExe = MakeFakeGitMm(root, "garbage output");
				Assert.Equal(GitMmVersionStatus.Unknown, GitMmVersionChecker.Check(junkExe).Status);
			}
			finally
			{
				try { Directory.Delete(root, recursive: true); } catch { }
			}
		}

		private static string MakeFakeGitMm(string root, string output)
		{
			string path = Path.Combine(root, "git-mm-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".sh");
			File.WriteAllText(path, "#!/bin/sh\ncat <<'EOF'\n" + output + "\nEOF\n");
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("chmod", "+x " + path)).WaitForExit();
			return path;
		}

		// ============================ 8) GitMm 工作区 tab ============================

		[Fact]
		public void GitMmWorkspace_TabOpensAndWarnsMissingCli()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpe2e_gitmm_ws_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string ws = Path.Combine(root, "ws");
			Directory.CreateDirectory(Path.Combine(ws, ".mm"));
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					// 预期弹窗：沙箱无 git-mm CLI → 打开工作区时 WarnIfGitMmUnavailable 弹 ErrorWindow
					HeadlessAppBootstrap.ExpectErrorDialogs();
					// OpenTab 不断言 RepositoryUserControl——GitMm 工作区经 TabManager 分流建 GitMm tab
					E2eMainWindowHarness.OpenTab(ws, out var window);

					// TabManager.OpenRepository 分流：应建 GitMm tab（GitMmUserControl 挂载）
					GitMmUserControl gitMm = window.GetVisualDescendants().OfType<GitMmUserControl>().FirstOrDefault();
					Assert.True(gitMm != null, "应创建 GitMmUserControl（GitMm 模式 tab）");
					Assert.Equal(ws, gitMm.WorkspacePath);
					Assert.StartsWith("git mm: ", gitMm.WorkspaceTitle);
					ScreenshotHelper.Snap(window, "12-gitmm-workspace-tab", ModuleDir);

					// WarnIfGitMmUnavailable（后台 Task → 200ms 看门狗滞后）：
					// 等 missing 警告弹窗被看门狗捕获（"git-mm" 中英译文均含，跨语言安全）
					Assert.True(UiClick.WaitFor(delegate
					{
						return HeadlessAppBootstrap.PeekCapturedErrorDialogs().Any(t => t.Contains("git-mm"));
					}), "应捕获 git-mm missing 警告弹窗（15s 超时）");
					string[] captured = HeadlessAppBootstrap.TakeCapturedErrorDialogs();
					// 全键经 TrFormat 断言（zh-Hans："未找到 git-mm 可执行文件 (git-mm.exe)。…"）
					Assert.Contains(E2eMainWindowHarness.TrFormat(
						"git-mm executable (git-mm.exe) was not found. git mm workspace features will be unavailable. Install git-mm 3.x and add it to PATH, or configure it in Preferences."),
						captured);

					// 关闭 GitMm tab（FindTab 按 WorkspacePath 匹配——与仓库 tab 同一收尾路径）
					E2eMainWindowHarness.CloseRepositoryTab(window, ws);
				});
			}
			finally
			{
				try { Directory.Delete(root, recursive: true); } catch { }
			}
		}

		// ============================ 9) LeanBranching Sync ============================

		[Fact]
		public void LeanBranching_SyncGuardsAndRealSync()
		{
			string repo = TestRepoFactory.CreateRemoteBehind();
			try
			{
				// 前置（守卫=分叉才拦）：fetch 后 origin/main=c2（Right≠0），再在 main 造一笔
				// 本地独有提交 c3（Left≠0）→ main 与上游分叉。AreInSync 语义（BehindAheadCount
				// Extensions）：Left==0（纯落后）或 Right==0（纯超前）都算同步——只有分叉才 false。
				// feature/x 检出态（activeBranch≠localMain）→ 触发 "You must sync" 守卫。
				TestRepoFactory.GitOutput(repo, "fetch -q origin");
				TestRepoFactory.GitOutput(repo, "checkout -q main");
				File.WriteAllText(Path.Combine(repo, "local.txt"), "local ahead\n");
				TestRepoFactory.GitOutput(repo, "add local.txt");
				TestRepoFactory.GitOutput(repo, "commit -q -m c3-local-ahead");
				TestRepoFactory.GitOutput(repo, "checkout -q -b feature/x main");
				Assert.NotEqual(GitOf(repo, "rev-parse main"), GitOf(repo, "rev-parse origin/main"));

				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForLoaded(repoControl, 2); // main + feature/x
						// LeanBranchingSyncCommand 前置链需要 CommitGraphCache（null 时静默 return）
						Assert.True(UiClick.WaitFor(delegate { return repoControl.CommitGraphCache != null; }),
							"CommitGraphCache 未装配（15s 超时）");
						WaitForActiveBranch(repoControl, "feature/x");

						// ===== 守卫路径：非 main 分支 + main 落后上游 → 错误弹窗（被测行为） =====
						HeadlessAppBootstrap.ExpectErrorDialogs();
						new LeanBranchingSyncCommand().Execute(repoControl);
						// Execute 同步弹 ErrorWindow，看门狗 200ms 滞后关闭——轮询等捕获（WaitFor 泵）
						Assert.True(UiClick.WaitFor(delegate
						{
							return HeadlessAppBootstrap.PeekCapturedErrorDialogs().Length >= 1;
						}), "应捕获 not-in-sync 守卫错误弹窗（15s 超时）");
						string[] guardErrors = HeadlessAppBootstrap.TakeCapturedErrorDialogs();
						Assert.Contains(E2eMainWindowHarness.TrFormat(
							"'{0}' is not in sync with '{1}'. You must checkout and sync '{0}' first.",
							"main", "origin/main"), guardErrors);
						// 守卫拦截：sync 未启动（无 .git/fork/sync 目录）
						Assert.False(Directory.Exists(Path.Combine(repo, ".git", "fork", "sync")),
							"守卫拦截时不应创建 sync 目录");

						// ===== 真实 Sync：回置"纯落后"（Left=0 → AreInSync，守卫因 active==localMain
					// 直接跳过）——Step1 rebase 快进 main 到 origin/main，orig==localMain 跳到
					// Step4/5（无子仓无 stash → 清理 sync 目录） =====
					TestRepoFactory.GitOutput(repo, "checkout -q main");
					TestRepoFactory.GitOutput(repo, "reset -q --hard origin/main~1"); // 丢弃 c3，回到 c1
					TestRepoFactory.GitOutput(repo, "branch -q -D feature/x");
					repoControl.InvalidateAndRefresh(SubDomain.References | SubDomain.Head);
					WaitForActiveBranch(repoControl, "main");

						new LeanBranchingSyncCommand().Execute(repoControl);
						Dispatcher.UIThread.RunJobs();
						E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
						Dispatcher.UIThread.RunJobs();

						// 终态：main 前进到 origin/main（Step1 rebase fast-forward）+ sync 目录清理（Step5）
						Assert.Equal(GitOf(repo, "rev-parse origin/main"), GitOf(repo, "rev-parse main"));
						Assert.False(Directory.Exists(Path.Combine(repo, ".git", "fork", "sync")),
							"Sync 完成后应清理 sync 目录");
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
