// E2E 模块15（2026-09-05）：远程交互 5 窗口（7 用例）。
// 覆盖：Fetch（origin 默认装配 + --all 开关预览/远程下拉禁用往返 + 真实 fetch：origin/main
// 前进而本地 main 不动）/Pull（upstream 默认装配（远程+远程分支+目标视图）+ --rebase 开关
// 预览 + 真实 pull：behind 拉平、b.txt 落地）/Push（default 上游项装配 + force-with-lease
// 预览与警告图标往返 + 真实 push：bare main 前进）/Push 新分支（new 项装配 + track 复选框
// 显示与 --set-upstream 预览 + 真实推送 + branch.<name>.remote 配置）/EditRemote Add 模式
// （空值/重复名三态禁用 + `git remote add` 预览 + 真实添加）/EditRemote Edit 模式（预填/
// 同值禁用 + set-url↔rename 双预览 + 真实改 URL）/AddCustomRefspec（remote/branch 预填 +
// OutRefspec 输出）。
// 模式：真实 MainWindow 打开 work 克隆（origin=本地 bare，全链路无网络）→ 生产构造器建
// 弹窗 → 控件树交互（UiClick.Toggle 走 IsCheckedChanged 生产管线 + TextBox.Text 直赋触发
// TextChanged）→ 终态轮询真实 git（fetch/pull/push 均为"入队即关"——关窗 ≠ 命令完成，
// 模块 11 教训：轮询 rev-parse/ls-remote 而非弹窗关闭即查；EditRemote 为 JobQueue 型
// "命令完成才关"（SubmitAndWaitClose 直接适用））。
// 截图走 2026-09-06 口径（ScreenshotHelper.Snap 内置）：主窗口 1920×1080，弹窗按自然
// 比例（本模块弹窗
// 比例（SizeToContent/固定尺寸 → 按真实形态截帧，不拉伸宽度）。
//
// 仓库形态（TestRepoFactory.CreateRemoteBehind/CreateBareRemote）：bare 远程 + work 克隆。
// behind 形态：other 克隆推 c2 后 work 未 fetch（fetch/pull 拉平它）；ahead 形态：work 本地
// c2 未推（push 推上去）。bare 路径 = <work 同级>/remote.git。
//
// 设置污染防护（模块 7/12/14 教训）：Fetch_FetchAllRemotes / FetchAllTags / Pull_Rebase /
// Pull_StashAndReapply / Push_PushAllTags 均在各自 OnSubmit 落盘——用例开头快照 + 显式归零
// 保证确定性初态（CheckBox.IsChecked 直接取自设置，污染值会让 Toggle(true) 因无变化不触发
// IsCheckedChanged），finally 恢复 + Save()。
//
// 轮询"将被创建的 ref"教训（模块15 首跑实证）：Push 新分支的终态是 bare 侧 refs/heads/feature
// 从无到有——轮询条件里不能直接 rev-parse 该 ref（推送在途时非零退出经 GitOutput 抛
// InvalidOperationException 炸出 WaitFor），须用 `ls-remote --heads origin`（恒 exit 0）+
// 行匹配（模块 11 多分支推送同款口径）。rev-parse 只适合轮询"已存在 ref 前进"（origin/main、
// bare main）的用例。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
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
	public class E2e15RemoteInteractionTests
	{
		private const string ModuleDir = "15-remote";

		// ============================ 共享助手 ============================

		/// <summary>work 克隆对应的 bare 远程路径（工厂固定布局：root/remote.git + root/work）。</summary>
		private static string BareOf(string work)
		{
			return Path.Combine(Directory.GetParent(work).FullName, "remote.git");
		}

		private static string GitOf(string repo, string args)
		{
			return TestRepoFactory.GitOutput(repo, args).Trim();
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

		/// <summary>点提交并等弹窗关闭。Fetch/Pull/Push 是"入队即关"（关窗 ≠ 命令完成，
		/// 终态须另行轮询）；EditRemote 是"命令完成才关"——本助手对两者都适用。</summary>
		private static void SubmitAndWaitClose(ForkPlusDialogWindow dialog, string what)
		{
			ForkPlusDialogFooter footer = FooterOf(dialog);
			UiClick.Click(footer.SubmitButton);
			Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
				what + "应在提交后关闭弹窗（15s 超时）");
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>等待 RepositoryData 的 remotes 装配到位（后台 git 读取经 Dispatcher 回 UI）。</summary>
		private static Remote OriginRemoteOf(RepositoryUserControl control)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return control.RepositoryData != null
					&& control.RepositoryData.Remotes != null
					&& control.RepositoryData.Remotes.Items.Any(r => r.Name == "origin");
			}), "remotes 应装配 origin（15s 超时）");
			return control.RepositoryData.Remotes.Items.First(r => r.Name == "origin");
		}

		// ============================ 1) Fetch ============================

		[Fact]
		public void Fetch_AllRemotesPreviewAndRealFetch()
		{
			string work = TestRepoFactory.CreateRemoteBehind();
			string bare = BareOf(work);
			bool savedFetchAllRemotes = ForkPlusSettings.Default.Fetch_FetchAllRemotes;
			bool savedFetchAllTags = ForkPlusSettings.Default.FetchAllTags;
			try
			{
				string c1Sha = GitOf(work, "rev-parse main");
				string c2Sha = GitOf(bare, "rev-parse main"); // other 推的远端新提交
				Assert.NotEqual(c1Sha, c2Sha);
				ForkPlusSettings.Default.Fetch_FetchAllRemotes = false;
				ForkPlusSettings.Default.FetchAllTags = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						var dialog = new FetchWindow(repoControl, repoControl.GitModule, null);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 默认装配：origin 选中 + 裸 fetch 预览
						Assert.Equal("origin", ((Remote)dialog.RemoteComboBox.SelectedItem).Name);
						Assert.True(footer.SubmitButton.IsEnabled, "有选中远程时提交应启用");
						Assert.Equal("git fetch origin", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "01-fetch-default", ModuleDir);

						// 勾选 Fetch all remotes → --all 预览 + 远程下拉禁用（IsCheckedChanged 生产管线）
						UiClick.Toggle(dialog.FetchAllRemotesCheckBox, true);
						Assert.Equal("git fetch --all", CommandPreviewOf(dialog));
						Assert.False(dialog.RemoteComboBox.IsEnabled, "fetch all 模式应禁用远程下拉");
						ScreenshotHelper.Snap(dialog, "02-fetch-all", ModuleDir);

						// 取消勾选 → 回落 origin 预览 + 下拉恢复
						UiClick.Toggle(dialog.FetchAllRemotesCheckBox, false);
						Assert.Equal("git fetch origin", CommandPreviewOf(dialog));
						Assert.True(dialog.RemoteComboBox.IsEnabled, "取消 fetch all 应恢复远程下拉");

						SubmitAndWaitClose(dialog, "fetch");

						// 真实仓库断言（入队即关 → 轮询终态）：origin/main 前进到 c2；
						// fetch 不动本地 main（c1 原地）、c2 的 b.txt 不落工作区
						Assert.True(UiClick.WaitFor(delegate
						{
							return GitOf(work, "rev-parse refs/remotes/origin/main") == c2Sha;
						}), "fetch 应使 origin/main 前进到远端 c2（15s 超时）");
						Assert.Equal(c1Sha, GitOf(work, "rev-parse main"));
						Assert.False(File.Exists(Path.Combine(work, "b.txt")),
							"fetch 只更新远程跟踪分支，b.txt 不应落工作区");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.Fetch_FetchAllRemotes = savedFetchAllRemotes;
				ForkPlusSettings.Default.FetchAllTags = savedFetchAllTags;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(work);
			}
		}

		// ============================ 2) Pull ============================

		[Fact]
		public void Pull_UpstreamDefaultAndRebasePreview()
		{
			string work = TestRepoFactory.CreateRemoteBehind();
			string bare = BareOf(work);
			bool savedRebase = ForkPlusSettings.Default.Pull_Rebase;
			bool savedStash = ForkPlusSettings.Default.Pull_StashAndReapply;
			bool savedFetchAllTags = ForkPlusSettings.Default.FetchAllTags;
			try
			{
				string c2Sha = GitOf(bare, "rev-parse main");
				ForkPlusSettings.Default.Pull_Rebase = false;
				ForkPlusSettings.Default.Pull_StashAndReapply = false;
				ForkPlusSettings.Default.FetchAllTags = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						var dialog = new PullWindow(repoControl, null);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// _referencesLoaded 是后台 Task（GetRemotes+GetReferences）完成后置位——
						// 提交按钮启用即装配完成
						Assert.True(UiClick.WaitFor(delegate { return footer.SubmitButton.IsEnabled; }),
							"Pull 弹窗应完成引用加载并启用提交（15s 超时）");

						// 默认装配：origin + 上游 origin/main + 目标视图 = 活跃分支 main
						Assert.Equal("origin", ((Remote)dialog.RemotesComboBox.SelectedItem).Name);
						RemoteBranch upstream = (RemoteBranch)dialog.RemoteBranchesComboBox.SelectedItem;
						Assert.Equal("refs/remotes/origin/main", upstream.FullReference);
						Assert.Equal("main", upstream.ShortName);
						Assert.Equal("main", ((LocalBranch)dialog.DestinationGitPointView.Value).Name);
						Assert.Equal("git pull origin main", CommandPreviewOf(dialog));

						// 勾选 rebase → 预览追加 --rebase；取消回落（IsCheckedChanged 生产管线）
						UiClick.Toggle(dialog.RebaseCheckBox, true);
						Assert.Equal("git pull origin main --rebase", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "03-pull-rebase", ModuleDir);
						UiClick.Toggle(dialog.RebaseCheckBox, false);
						Assert.Equal("git pull origin main", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "04-pull-default", ModuleDir);

						SubmitAndWaitClose(dialog, "pull");

						// 真实仓库断言（入队即关 → 轮询终态）：main 前进到 c2（fetch+merge 拉平）、b.txt 落地
						Assert.True(UiClick.WaitFor(delegate
						{
							return GitOf(work, "rev-parse main") == c2Sha;
						}), "pull 应使本地 main 前进到远端 c2（15s 超时）");
						Assert.Equal("behind change\n",
							File.ReadAllText(Path.Combine(work, "b.txt")).Replace("\r\n", "\n"));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.Pull_Rebase = savedRebase;
				ForkPlusSettings.Default.Pull_StashAndReapply = savedStash;
				ForkPlusSettings.Default.FetchAllTags = savedFetchAllTags;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(work);
			}
		}

		// ============================ 3) Push（已有上游） ============================

		[Fact]
		public void Push_UpstreamDefaultForceWarningAndRealPush()
		{
			string work = TestRepoFactory.CreateBareRemote(); // work 本地 main ahead 1
			string bare = BareOf(work);
			bool savedPushAllTags = ForkPlusSettings.Default.Push_PushAllTags;
			try
			{
				ForkPlusSettings.Default.Push_PushAllTags = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						var dialog = new PushWindow(repoControl);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);
						Assert.True(UiClick.WaitFor(delegate { return footer.SubmitButton.IsEnabled; }),
							"Push 弹窗应完成装配并启用提交（15s 超时）");

						// 默认装配：活跃 main + default 上游项 + 有上游时 track 复选框折叠
						Assert.Equal("main", ((LocalBranch)dialog.LocalBranchesComboBox.SelectedItem).Name);
						var remoteBranchItem = (PushWindow.RemoteBranchItem)dialog.RemoteBranchesComboBox.SelectedItem;
						Assert.Equal(E2eMainWindowHarness.TrFormat("default ({0})", "origin/main"),
							remoteBranchItem.Title);
						Assert.False(dialog.CreateTrackingReferenceCheckBox.IsVisible,
							"分支已有上游时 track 复选框应折叠");
						Assert.Equal("git push origin refs/heads/main", CommandPreviewOf(dialog));

						// 勾选 force → --force-with-lease 预览 + 警告图标；取消回落（往返验证）
						UiClick.Toggle(dialog.ForcePushCheckBox, true);
						Assert.Equal("git push --force-with-lease origin refs/heads/main", CommandPreviewOf(dialog));
						Assert.True(dialog.ForcePushWarningImage.IsVisible, "force 勾选应显示警告图标");
						ScreenshotHelper.Snap(dialog, "05-push-force", ModuleDir);
						UiClick.Toggle(dialog.ForcePushCheckBox, false);
						Assert.Equal("git push origin refs/heads/main", CommandPreviewOf(dialog));
						Assert.False(dialog.ForcePushWarningImage.IsVisible, "取消 force 应隐藏警告图标");
						ScreenshotHelper.Snap(dialog, "06-push-default", ModuleDir);

						SubmitAndWaitClose(dialog, "push");

						// 真实仓库断言（入队即关 → 轮询终态）：bare main 前进到本地 c2
						Assert.True(UiClick.WaitFor(delegate
						{
							return GitOf(bare, "rev-parse main") == GitOf(work, "rev-parse main");
						}), "push 应使 bare main 前进到本地 main（15s 超时）");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.Push_PushAllTags = savedPushAllTags;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(work);
			}
		}

		// ============================ 4) Push（新分支 set-upstream） ============================

		[Fact]
		public void Push_NewBranchSetUpsUpstream()
		{
			string work = TestRepoFactory.CreateBareRemote();
			string bare = BareOf(work);
			// 打开仓库前切到无上游新分支（RepositoryData 装配即 feature 活跃）
			TestRepoFactory.GitOutput(work, "checkout -q -b feature");
			bool savedPushAllTags = ForkPlusSettings.Default.Push_PushAllTags;
			try
			{
				ForkPlusSettings.Default.Push_PushAllTags = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						var dialog = new PushWindow(repoControl);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);
						Assert.True(UiClick.WaitFor(delegate { return footer.SubmitButton.IsEnabled; }),
							"Push 弹窗应完成装配并启用提交（15s 超时）");

						// 无上游分支装配：feature 选中 + track 复选框显示且默认勾上 + new 项
						Assert.Equal("feature", ((LocalBranch)dialog.LocalBranchesComboBox.SelectedItem).Name);
						Assert.True(dialog.CreateTrackingReferenceCheckBox.IsVisible,
							"分支无上游时 track 复选框应显示");
						Assert.True(dialog.CreateTrackingReferenceCheckBox.IsChecked.GetValueOrDefault(),
							"track 复选框应默认勾选");
						var remoteBranchItem = (PushWindow.RemoteBranchItem)dialog.RemoteBranchesComboBox.SelectedItem;
						Assert.Equal(E2eMainWindowHarness.TrFormat("new ({0})", "origin/feature"),
							remoteBranchItem.Title);
						Assert.Equal("git push --set-upstream origin refs/heads/feature", CommandPreviewOf(dialog));

						// 取消 track → 回落无 --set-upstream；重新勾选恢复（往返验证）
						UiClick.Toggle(dialog.CreateTrackingReferenceCheckBox, false);
						Assert.Equal("git push origin refs/heads/feature", CommandPreviewOf(dialog));
						UiClick.Toggle(dialog.CreateTrackingReferenceCheckBox, true);
						Assert.Equal("git push --set-upstream origin refs/heads/feature", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "07-push-newbranch", ModuleDir);

						SubmitAndWaitClose(dialog, "push 新分支");

						// 真实仓库断言（入队即关 → 轮询终态，模块11 多分支推送同款口径）：
						// 用 ls-remote 轮询（恒 exit 0），不用 rev-parse refs/heads/feature——
						// push 在途时该 ref 尚不存在，rev-parse 非零退出经 GitOutput 直接抛异常
						// 炸出轮询（模块15 首跑实证）。目标 sha 对上即 bare 侧 feature 已建。
						string featureSha = GitOf(work, "rev-parse feature");
						Assert.True(UiClick.WaitFor(delegate
						{
							string line = TestRepoFactory.GitOutput(work, "ls-remote --heads origin")
								.Split('\n', StringSplitOptions.RemoveEmptyEntries)
								.FirstOrDefault(l => l.EndsWith("refs/heads/feature"));
							return line != null && line.StartsWith(featureSha);
						}), "push 应在 bare 创建 refs/heads/feature（15s 超时）");
						// upstream 配置也须轮询：--set-upstream 在 bare ref 更新之后、push 进程退出前才写
						// branch.*.remote/merge，CI 高负载下调度延迟会拉开窗口——ls-remote 见到 ref 时
						// 本地配置可能尚未落盘。git config 键不存在退出码 1（GitOutput 会抛异常炸出轮询），
						// 用 GitOutputOrNull 容忍"暂无值"继续等，15s 内未写入才算失败。
						Assert.True(UiClick.WaitFor(delegate
						{
							return TestRepoFactory.GitOutputOrNull(work, "config branch.feature.remote") == "origin"
								&& TestRepoFactory.GitOutputOrNull(work, "config branch.feature.merge") == "refs/heads/feature";
						}), "push --set-upstream 应写入 branch.feature.remote/merge（15s 超时）");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.Push_PushAllTags = savedPushAllTags;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(work);
			}
		}

		// ============================ 5) EditRemote（Add 模式） ============================

		[Fact]
		public void EditRemote_AddModeValidationAndRealAdd()
		{
			string work = TestRepoFactory.CreateBareRemote();
			string bareUrl = BareOf(work);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						var dialog = new EditRemoteWindow(repoControl, repoControl.GitModule);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 1) 空远程名 → 禁用（显式设值覆盖构造器预填，剪贴板状态与本断言无关）
						dialog.RemoteNameTextBox.Text = string.Empty;
						dialog.RepositoryUrlTextBox.Text = bareUrl;
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "空远程名应禁用提交");

						// 2) 重复名（origin 已存在）→ 禁用 + 警告（英文全串直拼 SetStatus，非翻译键）
						dialog.RemoteNameTextBox.Text = "origin";
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "重复远程名应禁用提交");
						Assert.Equal("Remote 'origin' already exists", footer.StatusMessageTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "08-editremote-duplicate", ModuleDir);

						// 3) 合法新名 → 启用 + git remote add 预览（本地路径无空格不加引号）
						dialog.RemoteNameTextBox.Text = "backup";
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "合法新远程应启用提交");
						Assert.False(footer.StatusMessageTextBlock.IsVisible, "合法名应清除警告");
						Assert.Equal("git remote add backup " + bareUrl, CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "09-editremote-add", ModuleDir);

						// EditRemote 是"命令完成才关"（JobQueue 完成后 Close(result)）
						SubmitAndWaitClose(dialog, "添加 backup 远程");

						// 真实仓库断言：新远程存在且 URL 正确
						Assert.Equal(bareUrl, GitOf(work, "remote get-url backup"));
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

		// ============================ 6) EditRemote（Edit 模式） ============================

		[Fact]
		public void EditRemote_EditModeUrlPreviewAndRealChange()
		{
			string work = TestRepoFactory.CreateBareRemote();
			string bareUrl = BareOf(work);
			string root = Directory.GetParent(work).FullName;
			// 第二个 bare 作为新 URL（set-url 真实目标）
			string otherUrl = Path.Combine(root, "other.git");
			TestRepoFactory.GitOutput(work, "init -q --bare " + otherUrl);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						Remote origin = OriginRemoteOf(repoControl);
						var dialog = new EditRemoteWindow(repoControl, repoControl.GitModule, origin);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 预填当前名/URL；同值（无变化）禁用提交
						Assert.Equal("origin", dialog.RemoteNameTextBox.Text);
						Assert.Equal(origin.Url, dialog.RepositoryUrlTextBox.Text);
						Assert.False(footer.SubmitButton.IsEnabled, "名与 URL 均未变化应禁用提交");

						// 改 URL → set-url 预览
						dialog.RepositoryUrlTextBox.Text = otherUrl;
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "URL 变化应启用提交");
						Assert.Equal("git remote set-url origin " + otherUrl, CommandPreviewOf(dialog));

						// URL 恢复 + 改名 → rename 预览（另一种变更形态；不提交，仅预览口径）
						dialog.RepositoryUrlTextBox.Text = origin.Url;
						dialog.RemoteNameTextBox.Text = "origin2";
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "改名应启用提交");
						Assert.Equal("git remote rename origin origin2", CommandPreviewOf(dialog));

						// 回到 set-upstream 场景提交：名恢复 origin、URL 指向 other.git
						dialog.RemoteNameTextBox.Text = "origin";
						dialog.RepositoryUrlTextBox.Text = otherUrl;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git remote set-url origin " + otherUrl, CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "10-editremote-editurl", ModuleDir);

						SubmitAndWaitClose(dialog, "修改 origin URL");

						// 真实仓库断言：origin URL 已切换到 other.git
						Assert.Equal(otherUrl, GitOf(work, "remote get-url origin"));
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

		// ============================ 7) AddCustomRefspec ============================

		[Fact]
		public void AddCustomRefspec_PrefillAndOutRefspec()
		{
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					// 独立小窗口：无 git 执行，构造入参即预填
					var dialog = new AddCustomRefspecWindow("origin", "feature");
					dialog.Show();
					Dispatcher.UIThread.RunJobs();
					ForkPlusDialogFooter footer = FooterOf(dialog);

					// 预填：远程名前缀 + 本地分支名（PushWindow "Custom..." 子流程的默认值）
					Assert.Equal("origin/", dialog.RemoteNameTextBlock.Text);
					Assert.Equal("feature", dialog.BranchNameTextBox.Text);
					Assert.True(footer.SubmitButton.IsEnabled, "自定义 refspec 提交应默认启用");

					// 输入自定义目标名 → 提交（CloseWithOk）→ OutRefspec 输出该名
					dialog.BranchNameTextBox.Text = "custom-dest";
					Dispatcher.UIThread.RunJobs();
					ScreenshotHelper.Snap(dialog, "11-custom-refspec", ModuleDir);
					SubmitAndWaitClose(dialog, "添加自定义 refspec");
					Assert.Equal("custom-dest", dialog.OutRefspec);
				});
			}
			finally
			{
				// 无临时仓库需要清理
			}
		}
	}
}
