// E2E 模块12（2026-09-05）：标签操作 5 窗口（7 用例）。
// 覆盖：创建（校验三态 + 消息/无消息命令预览 + 真实建附注标签）/创建并推送（远程真实验证）/
// 删除单标签（含"从远程删除"两态预览 + 真实删两端）/删除多标签（列表模式）/单标签推送/
// 多标签推送/标签详情（附注 + 轻量两形态）。
// 模式：E2eMainWindowHarness.OpenRepository（真实 MainWindow 生产入口）→ 生产构造器建弹窗 →
// 控件树交互（UiClick.Click/Toggle + 属性直设走生产 TextChanged 管线）→ WaitFor 弹窗关闭
// （JobQueue 后台命令完成 → Close）→ TestRepoFactory.GitOutput 真实 git 状态断言。
// 截图走 2026-09-06 口径（ScreenshotHelper.Snap 内置）：主窗口 1920×1080，弹窗按自然比例。
//
// 时序口径（模块 11 教训沿用）：
// - CreateTagWindow / RemoveTagWindow 是 AddUndoable/JobQueue 型"命令完成才关"——
//   SubmitAndWaitClose 直接适用；
// - PushTagWindow / PushMultipleTagsWindow 是"入队即关"（JobQueue.Add 后同步 Close()）——
//   终态断言必须轮询 ls-remote --tags 等后台推送完成，不可弹窗关闭即查。
//
// TagDetailsWindow 语义判定（探针实证 2026-09-05，非迁移回归，按原版语义断言）：
//   bt_get_tag_details 只接受 tag 对象 oid（传剥壳 commit sha 报 NotFound——探针三种输入
//   直测：tag对象sha=成功返回结构化 tagger；剥壳sha/轻量sha=失败）。而侧栏/提交列表的 Tag
//   由 RepositoryReferences.New 装配，其 dereferencedShaString 参数恒传同一个（剥壳）sha
//   （Reference.Create 语义：targetObjectSha=ref直接指向对象、sha=剥壳对象——但两仓的
//   RepositoryReferences.cs 逐字节一致，都传同 sha → TargetObjectSha 永远=剥壳 sha），
//   → bt_get_tag_details 必失败 → 走 for-each-ref 回退：tagger 三字段空、消息区=
//   "%(taggername) %(taggeremail) %(taggerdate)\n\n%(contents)" 全文（附注）或提交消息（轻量）。
//   WPF 原仓 C# 链路逐字节一致（TagDetailsWindow/GetTagMessageGitCommand/RepositoryReferences/
//   Reference 全 diff 实证），且 Windows 原版 bt_get_references 也必须返回剥壳 sha（否则
//   ReferencesBySha 键不上 commit sha，附注标签永远挂不到提交图节点——Fork 核心功能不可
//   能坏），故该回退行为是两仓一致的原始行为，if 分支在侧栏链路两仓都不可达。
using System;
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
	public class E2e12TagOperationsTests
	{
		private const string ModuleDir = "12-tagops";

		// ============================ 共享助手 ============================

		/// <summary>等引用装配完成且标签就位（后台 git 读取经 Dispatcher 回 UI）。</summary>
		private static RepositoryReferences WaitForTags(RepositoryUserControl control, int minTags)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return control.RepositoryData != null
					&& control.RepositoryData.References.LocalBranches.Length >= 1
					&& control.RepositoryData.References.Tags.Length >= minTags
					&& control.RepositoryStatus != null;
			}), "引用/标签/工作区状态未装配（15s 超时）");
			return control.RepositoryData.References;
		}

		private static Tag TagNamed(RepositoryReferences references, string name)
		{
			return references.Tags.First(t => t.Name == name);
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

		/// <summary>远程标签集（ls-remote 直查 bare 远程，本地 refs/tags 可能残留未 prune，不作依据）。</summary>
		private static string RemoteTags(string repo)
		{
			return TestRepoFactory.GitOutput(repo, "ls-remote --tags origin");
		}

		// ============================ 1) 创建标签 ============================

		[Fact]
		public void CreateTag_ValidationStates_AndCreate()
		{
			string repo = TestRepoFactory.CreateTags();
			bool savedPush = ForkPlusSettings.Default.CreateTag_Push;
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForTags(repoControl, 2);
						Assert.Equal("main", references.ActiveBranch.Name);

						var dialog = new CreateTagWindow(repoControl.GitModule, references,
							repoControl.RepositoryData.Remotes.Items, references.ActiveBranch);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);
						Assert.Same(references.ActiveBranch, dialog.GitPointView.Value);

						// 1) 空名 → 禁用（无消息预览：GetCommandPreview 对空白名返回 null）
						dialog.TagNameTextBox.Text = string.Empty;
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "空标签名应禁用提交");
						ScreenshotHelper.Snap(dialog, "01-create-tag-initial", ModuleDir);

						// 2) 重复名 → 禁用 + 警告（全串拼接直传 SetStatus，Translate 的
						//    TranslatePattern 格式键回退命中 "Tag '{0}' already exists"
						//    ——PreferencesLocalization.cs 显式 ReplacePattern，两仓一致）
						dialog.TagNameTextBox.Text = "ann-1.0";
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "重复标签名应禁用提交");
						Assert.Equal(E2eMainWindowHarness.TrFormat("Tag '{0}' already exists", "ann-1.0"),
							footer.StatusMessageTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "02-create-tag-duplicate-warning", ModuleDir);

						// 3) 非法名（':'）→ ReferenceNameValidator 警告 + 禁用（与模块 11 分支名同口径：
						//    两仓校验器规则集相同，空格不拦属原始行为）
						dialog.TagNameTextBox.Text = "bad:name";
						Dispatcher.UIThread.RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "非法标签名应禁用提交");
						Assert.True(footer.StatusMessageTextBlock.IsVisible, "非法名应有可见警告状态");

						// 4) 合法新名 → 启用；命令预览：无消息 `git tag -a <name> main`，
						//    有消息 `git tag -a -m "<msg>" <name> main`（含空格消息加引号）
						dialog.TagNameTextBox.Text = "v3.0.0";
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "合法新标签名应启用提交");
						Assert.Equal("git tag -a v3.0.0 main", CommandPreviewOf(dialog));

						dialog.TagMessageTextBox.Text = "release notes";
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git tag -a -m \"release notes\" v3.0.0 main", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "03-create-tag-command-preview", ModuleDir);

						// 5) 提交（推送开关显式关——CreateTag_Push 持久化设置可能被先前用例污染，
						//    且本仓库无远程，推送循环为空操作但会落盘设置，finally 统一恢复）
						UiClick.Toggle(dialog.PushCheckBox, false);
						Assert.True(footer.SubmitButton.IsEnabled, "推送关时合法名仍应启用提交");
						SubmitAndWaitClose(dialog, "创建标签");

						// 真实仓库断言：附注标签（对象类型 tag）+ 消息 + 指向 main HEAD
						Assert.Equal("tag", TestRepoFactory.GitOutput(repo, "cat-file -t v3.0.0").Trim());
						Assert.Equal("release notes",
							TestRepoFactory.GitOutput(repo, "for-each-ref refs/tags/v3.0.0 --format=%(contents)").Trim());
						Assert.Equal(
							TestRepoFactory.GitOutput(repo, "rev-parse main").Trim(),
							TestRepoFactory.GitOutput(repo, "rev-parse v3.0.0^{}").Trim());
						// 既有标签不受影响
						Assert.Equal("ann-1.0 light-2.0 v3.0.0",
							string.Join(" ", TestRepoFactory.GitOutput(repo, "tag").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.CreateTag_Push = savedPush;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 2) 创建并推送 ============================

		[Fact]
		public void CreateTag_WithPush_UploadsToRemote()
		{
			string repo = TestRepoFactory.CreateRemoteTags();
			bool savedPush = ForkPlusSettings.Default.CreateTag_Push;
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForTags(repoControl, 5);

						var dialog = new CreateTagWindow(repoControl.GitModule, references,
							repoControl.RepositoryData.Remotes.Items, references.ActiveBranch);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						dialog.TagNameTextBox.Text = "rel-new";
						dialog.TagMessageTextBox.Text = "pushed release";
						Dispatcher.UIThread.RunJobs();

						// 推送开关：预览追加 `git push <remote> refs/tags/<name>` 行（单远程）
						UiClick.Toggle(dialog.PushCheckBox, true);
						Assert.Contains("git tag -a -m \"pushed release\" rel-new main", CommandPreviewOf(dialog));
						Assert.Contains("git push origin refs/tags/rel-new", CommandPreviewOf(dialog));
						Assert.True(footer.SubmitButton.IsEnabled, "创建并推送应启用提交");
						ScreenshotHelper.Snap(dialog, "04-create-tag-push-preview", ModuleDir);

						// 提交 → CreateTagGitCommand + PushTagGitCommand（AddUndoable 型：
						// 命令完成才关窗，关窗即可断言——但推送在 JobQueue 后台线程，
						// 关窗与推送完成同步发生，仍轮询兜底）
						SubmitAndWaitClose(dialog, "创建并推送标签");
						Assert.True(UiClick.WaitFor(delegate
						{
							return RemoteTags(repo).Contains("refs/tags/rel-new");
						}), "后台推送应在超时内把标签送上远程（15s 超时）");

						// 本地 + 远程双端就位，既有远程标签不动
						Assert.Equal("tag", TestRepoFactory.GitOutput(repo, "cat-file -t rel-new").Trim());
						string remoteTags = RemoteTags(repo);
						Assert.Contains("refs/tags/rel-1", remoteTags);
						Assert.Contains("refs/tags/rel-2", remoteTags);
						Assert.DoesNotContain("refs/tags/rel-3", remoteTags);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.CreateTag_Push = savedPush;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 3) 删除单标签（含远程删除） ============================

		[Fact]
		public void RemoveTag_Single_DeletesLocalAndRemote()
		{
			string repo = TestRepoFactory.CreateRemoteTags();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForTags(repoControl, 5);
						Tag rel1 = TagNamed(references, "rel-1");

						var dialog = new RemoveTagWindow(repoControl, new Tag[] { rel1 }, references);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 单标签模式：GitPointView 单视图装配（GitPoints 列表容器折叠——
						// 构造器按数量分流的既定设计，与模块 11 RemoveRemoteBranch 同款）
						Assert.Same(rel1, dialog.GitPointView.Value);
						Assert.False(dialog.GitPointsContainer.IsVisible, "单标签模式应折叠列表容器");

						// 默认仅本地删除：`git tag -d rel-1`
						Assert.True(footer.SubmitButton.IsEnabled, "删除标签提交应启用");
						Assert.Contains("git tag -d rel-1", CommandPreviewOf(dialog));

						// 勾选"从远程删除"：追加 `git push origin --delete rel-1`
						UiClick.Toggle(dialog.DeleteFromRemotesCheckBox, true);
						Assert.Contains("git tag -d rel-1", CommandPreviewOf(dialog));
						Assert.Contains("git push origin --delete rel-1", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "05-remove-tag-remote-delete", ModuleDir);

						SubmitAndWaitClose(dialog, "删除标签（本地+远程）");

						// 本地端 + 远程端（ls-remote 直查 bare）双验证；rel-2 不受影响
						Assert.Equal("", TestRepoFactory.GitOutput(repo, "tag -l rel-1").Trim());
						string remoteTags = RemoteTags(repo);
						Assert.DoesNotContain("refs/tags/rel-1", remoteTags);
						Assert.Contains("refs/tags/rel-2", remoteTags);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 4) 删除多标签（列表模式） ============================

		[Fact]
		public void RemoveTags_MultiMode_ListsAndDeletesBoth()
		{
			string repo = TestRepoFactory.CreateTags();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForTags(repoControl, 2);

						var dialog = new RemoveTagWindow(repoControl,
							new Tag[] { TagNamed(references, "ann-1.0"), TagNamed(references, "light-2.0") }, references);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 多标签模式：GitPoints 列表装配 2 项（单视图折叠，与单标签模式互补）
						Tag[] listed = dialog.GitPoints.ItemsSource.OfType<Tag>().ToArray();
						Assert.Equal(2, listed.Length);
						Assert.Contains(listed, t => t.Name == "ann-1.0");
						Assert.Contains(listed, t => t.Name == "light-2.0");
						Assert.False(dialog.GitPointView.IsVisible, "多标签模式应折叠单视图");
						Assert.True(footer.SubmitButton.IsEnabled, "删除多标签提交应启用");
						Assert.Contains("git tag -d ann-1.0 light-2.0", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "06-remove-tags-multi", ModuleDir);

						SubmitAndWaitClose(dialog, "删除多标签");
						// 本仓库无远程（远程删除开关保持默认关），仅本地两端全删
						Assert.Equal("", TestRepoFactory.GitOutput(repo, "tag").Trim());
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 5) 单标签推送 ============================

		[Fact]
		public void PushTag_UploadsSingleTag()
		{
			string repo = TestRepoFactory.CreateRemoteTags();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForTags(repoControl, 5);
						Tag rel3 = TagNamed(references, "rel-3");

						var dialog = new PushTagWindow(repoControl, rel3, null);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 单远程仓库：ComboBox 自动选中 origin（upstream 推导）→ 提交启用
						Assert.Same(rel3, dialog.TagGitPointView.Value);
						Assert.True(footer.SubmitButton.IsEnabled, "选中远程后推送应启用");
						Assert.Equal("git push origin refs/tags/rel-3", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "07-push-tag", ModuleDir);

						// 入队即关型（模块 11 PushMultipleBranches 同款时序）：关窗 ≠ 推送完成
						UiClick.Click(footer.SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
							"入队后应立即关闭弹窗（15s 超时）");
						Assert.True(UiClick.WaitFor(delegate
						{
							return RemoteTags(repo).Contains("refs/tags/rel-3");
						}), "后台推送应在超时内把 rel-3 送上远程（15s 超时）");

						// 只推了 rel-3，其余本地标签不上远程
						string remoteTags = RemoteTags(repo);
						Assert.DoesNotContain("refs/tags/rel-4", remoteTags);
						Assert.DoesNotContain("refs/tags/rel-5", remoteTags);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 6) 多标签推送 ============================

		[Fact]
		public void PushMultipleTags_UploadsBoth()
		{
			string repo = TestRepoFactory.CreateRemoteTags();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForTags(repoControl, 5);

						var dialog = new PushMultipleTagsWindow(repoControl,
							new Tag[] { TagNamed(references, "rel-4"), TagNamed(references, "rel-5") }, null);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 列表装配（映射为名字）+ 预览（两 refspec 一次 push）
						string[] names = dialog.TagsItemsControl.ItemsSource.OfType<string>().ToArray();
						Assert.Equal(new[] { "rel-4", "rel-5" }, names);
						Assert.True(footer.SubmitButton.IsEnabled, "选中远程后多标签推送应启用");
						Assert.Equal("git push origin refs/tags/rel-4 refs/tags/rel-5", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "08-push-tags-multi", ModuleDir);

						// 入队即关型：轮询 ls-remote 等后台推送
						UiClick.Click(footer.SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
							"入队后应立即关闭弹窗（15s 超时）");
						Assert.True(UiClick.WaitFor(delegate
						{
							string tags = RemoteTags(repo);
							return tags.Contains("refs/tags/rel-4") && tags.Contains("refs/tags/rel-5");
						}), "后台推送应在超时内把两标签送上远程（15s 超时）");

						// rel-3 仍只在本地（本用例没推它）
						Assert.DoesNotContain("refs/tags/rel-3", RemoteTags(repo));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 7) 标签详情 ============================

		[Fact]
		public void TagDetails_AnnotatedAndLightweight()
		{
			string repo = TestRepoFactory.CreateTags();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForTags(repoControl, 2);
						Tag ann = TagNamed(references, "ann-1.0");
						Tag light = TagNamed(references, "light-2.0");

						// 附注标签：两仓一致走 for-each-ref 回退（见文件头语义判定）——
						// tagger 三字段空；消息=tagger 行+空行+tag 消息全文。
						// 期望值用与生产完全相同的 for-each-ref 命令计算（GitPsi 与 GitRequest
						// 同为单 Arguments 串 → 同一 .NET 解析 → 同一 argv，输出逐字节一致）
						var annDetails = new TagDetailsWindow(repoControl.GitModule, ann);
						annDetails.Show();
						Dispatcher.UIThread.RunJobs();

						Assert.Same(ann, annDetails.GitPointView.Value);
						Assert.Equal("", annDetails.TaggerTextBlock.Text);
						Assert.Equal("", annDetails.TaggerEmailTextBlock.Text);
						Assert.Equal("", annDetails.TaggerDateTextBlock.Text);
						string expectedAnn = TestRepoFactory.GitOutput(repo,
							"for-each-ref --format=\"%(taggername) %(taggeremail) %(taggerdate)%0a%0a%(contents)\" refs/tags/ann-1.0").Trim();
						Assert.Equal(expectedAnn, annDetails.TagDetailsTextBox.Text);
						ScreenshotHelper.Snap(annDetails, "09-tag-details-annotated", ModuleDir);

						// 轻量标签：无 tagger → 消息= %(contents) = 提交消息（Trim 后）
						var lightDetails = new TagDetailsWindow(repoControl.GitModule, light);
						lightDetails.Show();
						Dispatcher.UIThread.RunJobs();

						Assert.Same(light, lightDetails.GitPointView.Value);
						Assert.Equal("", lightDetails.TaggerTextBlock.Text);
						Assert.Equal("", lightDetails.TaggerEmailTextBlock.Text);
						Assert.Equal("", lightDetails.TaggerDateTextBlock.Text);
						string expectedLight = TestRepoFactory.GitOutput(repo,
							"for-each-ref --format=\"%(taggername) %(taggeremail) %(taggerdate)%0a%0a%(contents)\" refs/tags/light-2.0").Trim();
						Assert.Equal(expectedLight, lightDetails.TagDetailsTextBox.Text);
						Assert.Equal("second commit", lightDetails.TagDetailsTextBox.Text);
						ScreenshotHelper.Snap(lightDetails, "10-tag-details-lightweight", ModuleDir);
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
