// E2E 模块14（2026-09-05）：Stash 5 窗口（7 用例）。
// 覆盖：保存（空态/消息/Stage new files 三态命令预览 + 真实 stash：跟踪改动与已暂存新文件
// 一并入栈、工作区还原干净）/部分保存（文件列表装配 + filesToStash 预勾选 + 行内 CheckBox
// 取消勾选禁用提交 + pathspec 预览 + 真实部分 stash：仅所选文件回滚、其余改动原样）/
// 应用（apply 保留条目 + pop 删除条目两态预览往返 + 警告图标联动 + 真实应用：--index 连
// 暂存态一并恢复）/删除（单模式 GitPointView 视图 + 多模式 GitPoints 列表 + 批量 drop
// 真实执行清空）/重命名（预填当前消息/同值禁用 + `git stash rename` 预览 + 真实重命名：
// 新消息入栈、条目数不变、OutResultSha 与 stash@{0} 一致）。
// 模式：E2eMainWindowHarness.OpenRepository（真实 MainWindow 生产入口）→ 生产构造器建弹窗 →
// 控件树交互（UiClick.Toggle 走 IsCheckedChanged 生产管线 + TextBox.Text 直赋触发 TextChanged）→
// WaitFor 弹窗关闭（JobQueue/AddUndoable 后台命令完成 → Close）→ TestRepoFactory.GitOutput
// 真实 git 状态断言（stash list --format=%s / rev-parse / status --porcelain / 文件系统）。
// 截图走 2026-09-06 口径（ScreenshotHelper.Snap 内置）：主窗口 1920×1080，弹窗按自然
// 比例（本模块弹窗 SizeToContent/固定尺寸 → 按真实形态截帧，不拉伸宽度）。
//
// 时序口径（模块 11/12/13 教训沿用）：
// - 本模块 5 个弹窗的 OnSubmit 全部为 JobQueue/AddUndoable 型"命令完成才关"——
//   SubmitAndWaitClose 直接适用（与模块 13 的 Merge/Rebase 同款）；
// - stash 条目对象（StashRevision）取自 repoControl.RepositoryData.Stashes（生产侧栏同源：
//   RefreshRepositoryDataGitCommand → GetStashesGitCommand 真实管线装配，WaitForStashes 轮询
//   到位后再建弹窗，避免拿空列表）。
//
// 命令预览口径：预览是展示语义（SaveStash 恒 `git stash push` 基底 + -m/--include-untracked
// 追加；Apply 为 apply|pop 切换；Rename 为应用级合成命令 `git stash rename`——实际执行走
// RenameStashGitCommand 的 commit-tree + stash store + drop 复合序列），用例按真实 git 结果
// 断言执行效果、按预览字面断言展示，两者分开。
//
// 设置污染防护（模块 7/12 教训）：SaveStash_StageNewFiles / ApplyStash_DeleteAfterApply
// 在 OnSubmit 落盘持久化——用例开头快照 + 显式归零保证确定性初态（CheckBox.IsChecked 直接
// 取自设置，污染值会让 Toggle(true) 因无变化不触发 IsCheckedChanged），finally 恢复 + Save()。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e14StashTests
	{
		private const string ModuleDir = "14-stash";

		// ============================ 共享助手 ============================

		/// <summary>等 stash 列表装配到位（RepositoryData.Stashes 生产管线，后台 git 读取经 Dispatcher 回 UI）。</summary>
		private static RepositoryStashes WaitForStashes(RepositoryUserControl control, int expectedCount)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return control.RepositoryData != null
					&& control.RepositoryData.Stashes != null
					&& control.RepositoryData.Stashes.Count == expectedCount;
			}), "stash 列表应装配 " + expectedCount + " 条（15s 超时）");
			return control.RepositoryData.Stashes;
		}

		/// <summary>按 reflog 名取条目（stash@{0}=栈顶最新，CreateStash 工厂里即 stash-two）。</summary>
		private static StashRevision StashNamed(RepositoryStashes stashes, string reflogName)
		{
			StashRevision stash = stashes.Items.FirstOrDefault(s => s.ReflogName == reflogName);
			Assert.True(stash != null, "应存在条目 " + reflogName + "（实际条目: "
				+ string.Join(", ", stashes.Items.Select(s => s.ReflogName)) + "）");
			return stash;
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

		/// <summary>点提交并等弹窗关闭（JobQueue/AddUndoable 后台命令完成 → Close(result)）。</summary>
		private static void SubmitAndWaitClose(ForkPlusDialogWindow dialog, string what)
		{
			ForkPlusDialogFooter footer = FooterOf(dialog);
			UiClick.Click(footer.SubmitButton);
			Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
				what + "应在命令完成后关闭弹窗（15s 超时）");
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>stash 条目主题（栈顶→栈底）。语言无关的纯 git 事实断言。</summary>
		private static string[] StashSubjectsOf(string repo)
		{
			return TestRepoFactory.GitOutput(repo, "stash list --format=%s")
				.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.ToArray();
		}

		/// <summary>生产命令取工作区变更文件（CreatePartialStashWindow 构造入参的同一数据源）。</summary>
		private static ChangedFile[] ChangedFilesOf(GitModule gitModule)
		{
			GitCommandResult<ChangedFilesCollection> result = new GetChangedFilesGitCommand().Execute(gitModule);
			Assert.True(result.Succeeded, "GetChangedFilesGitCommand 应成功: " + (result.Error?.FriendlyDescription ?? ""));
			return result.Result.ChangedFiles;
		}

		/// <summary>部分保存列表行内文件勾选框（ContainerFromItem → 视觉树第一个 CheckBox，
		/// 程序化勾选走 TwoWay 绑定回写 ViewModel.Selected + IsCheckedChanged 生产管线）。</summary>
		private static CheckBox ItemCheckBoxOf(CreatePartialStashWindow dialog, PartialStashFileViewModel item)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return dialog.PartialStashListBox.ContainerFromItem(item) != null;
			}), "列表项容器应完成物化");
			var container = dialog.PartialStashListBox.ContainerFromItem(item);
			CheckBox checkBox = container.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault();
			Assert.NotNull(checkBox);
			return checkBox;
		}

		// ============================ 1) 保存 stash ============================

		[Fact]
		public void SaveStash_MessageAndUntracked_CreatesCleanStash()
		{
			string repo = TestRepoFactory.CreateStashWork();
			bool savedStageNewFiles = ForkPlusSettings.Default.SaveStash_StageNewFiles;
			try
			{
				// 确定性初态（设置可能被历史运行污染，见类头"设置污染防护"）
				ForkPlusSettings.Default.SaveStash_StageNewFiles = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new SaveStashWindow(repoControl.GitModule);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 初始：空消息 + 不暂存新文件 → 裸 git stash push
						Assert.True(footer.SubmitButton.IsEnabled, "保存 stash 提交应默认启用");
						Assert.Equal("git stash push", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "01-save-default", ModuleDir);

						// 输入消息 → 预览加 -m "..."（TextChanged → RefreshCommandPreview）
						dialog.StashMessageTextBox.Text = "wip stash";
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git stash push -m \"wip stash\"", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "02-save-message", ModuleDir);

						// 勾选 Stage new files → 预览追加 --include-untracked（IsCheckedChanged 生产管线）
						UiClick.Toggle(dialog.StageNewFilesCheckBox, true);
						Assert.Equal("git stash push -m \"wip stash\" --include-untracked", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "03-save-untracked", ModuleDir);

						SubmitAndWaitClose(dialog, "保存 stash（含新文件）");

						// 真实仓库断言：1 条 stash（消息入栈）；预览虽写 push，实际执行走
						// SaveStashGitCommand（stage 新文件 + git stash save）——跟踪改动与
						// 已暂存新文件全部入栈，工作区还原干净
						// （git stash push -m 的 subject 标准格式 "On {branch}: {msg}"，
						//  UI 侧 StashRevision.Message 解析时剥前缀，裸 git 输出带前缀）
						Assert.Equal(new[] { "On main: wip stash" }, StashSubjectsOf(repo));
						Assert.Equal("a base\n", File.ReadAllText(Path.Combine(repo, "a.txt")).Replace("\r\n", "\n"));
						Assert.Equal("b base\n", File.ReadAllText(Path.Combine(repo, "b.txt")).Replace("\r\n", "\n"));
						Assert.False(File.Exists(Path.Combine(repo, "c.txt")),
							"已暂存的新文件随 stash 入栈，c.txt 不应留在工作区");
						Assert.Equal("", TestRepoFactory.GitOutput(repo, "status --porcelain").Trim());
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.SaveStash_StageNewFiles = savedStageNewFiles;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 2) 部分保存 stash ============================

		[Fact]
		public void PartialStash_OnlySelectedFileStashed()
		{
			string repo = TestRepoFactory.CreateStashWork();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 生产数据源：GetChangedFilesGitCommand（窗口构造入参的同一命令）
						ChangedFile[] all = ChangedFilesOf(repoControl.GitModule);
						Assert.Equal(3, all.Length); // a.txt 修改 / b.txt 修改 / c.txt 未跟踪
						ChangedFile[] toStash = all.Where(f => f.Path == "a.txt").ToArray();
						Assert.Single(toStash);

						var dialog = new CreatePartialStashWindow(repoControl.GitModule, toStash, all);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 列表装配：3 文件自然序，filesToStash 中的 a.txt 预勾选
						PartialStashFileViewModel[] items = dialog.PartialStashListBox.ItemsSource
							.OfType<PartialStashFileViewModel>().ToArray();
						Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" },
							items.Select(i => i.FilePath).ToArray());
						Assert.True(items[0].Selected, "filesToStash 中的 a.txt 应预勾选");
						Assert.False(items[1].Selected, "b.txt 不在 filesToStash，应未勾选");
						Assert.False(items[2].Selected, "c.txt 不在 filesToStash，应未勾选");
						Assert.True(footer.SubmitButton.IsEnabled, "有选中文件提交应启用");
						Assert.Equal("git stash push -- a.txt", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "04-partial-selection", ModuleDir);

						// 输入消息 → 预览加 -m "..."（含空格加引号）
						dialog.StashMessageTextBox.Text = "partial wip";
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git stash push -m \"partial wip\" -- a.txt", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "05-partial-message", ModuleDir);

						// 行内 CheckBox 取消勾选 a.txt → 提交禁用、预览清空（IsSubmitAllowed）
						CheckBox aCheckBox = ItemCheckBoxOf(dialog, items[0]);
						UiClick.Toggle(aCheckBox, false);
						Assert.False(footer.SubmitButton.IsEnabled, "无选中文件应禁用提交");
						Assert.Equal("", CommandPreviewOf(dialog));

						// 重新勾选 → 恢复启用（TwoWay 绑定回写 Selected）
						UiClick.Toggle(aCheckBox, true);
						Assert.True(footer.SubmitButton.IsEnabled, "重新勾选应恢复启用提交");

						SubmitAndWaitClose(dialog, "部分 stash（仅 a.txt）");

						// 真实仓库断言：仅 a.txt 入栈回滚；b.txt 改动与 c.txt 原样未动
						Assert.Equal(new[] { "On main: partial wip" }, StashSubjectsOf(repo));
						Assert.Equal("a base\n", File.ReadAllText(Path.Combine(repo, "a.txt")).Replace("\r\n", "\n"));
						Assert.Equal("b modified\n", File.ReadAllText(Path.Combine(repo, "b.txt")).Replace("\r\n", "\n"));
						Assert.True(File.Exists(Path.Combine(repo, "c.txt")), "未选中的 c.txt 不应被动");
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

		// ============================ 3) 应用 stash（apply 保留条目） ============================

		[Fact]
		public void ApplyStash_ApplyKeepsEntry()
		{
			string repo = TestRepoFactory.CreateStash();
			bool savedDeleteAfterApply = ForkPlusSettings.Default.ApplyStash_DeleteAfterApply;
			try
			{
				ForkPlusSettings.Default.ApplyStash_DeleteAfterApply = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryStashes stashes = WaitForStashes(repoControl, 2);
						StashRevision top = StashNamed(stashes, "stash@{0}");
						Assert.Equal("stash-two", top.Message);

						var dialog = new ApplyStashWindow(repoControl, top);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 装配 + 默认 apply 预览（保留条目）
						Assert.Same(top, dialog.GitPointView.Value);
						Assert.True(footer.SubmitButton.IsEnabled, "应用 stash 提交应默认启用");
						Assert.Equal("git stash apply stash@{0}", CommandPreviewOf(dialog));

						// 勾选删除 → pop 预览 + 警告图标；取消勾选 → 回落 apply（往返验证）
						UiClick.Toggle(dialog.DeleteStashAfterApplyCheckBox, true);
						Assert.Equal("git stash pop stash@{0}", CommandPreviewOf(dialog));
						Assert.True(dialog.DeleteStashWarningImage.IsVisible, "勾选删除应显示警告图标");
						UiClick.Toggle(dialog.DeleteStashAfterApplyCheckBox, false);
						Assert.Equal("git stash apply stash@{0}", CommandPreviewOf(dialog));
						Assert.False(dialog.DeleteStashWarningImage.IsVisible, "取消勾选应隐藏警告图标");
						ScreenshotHelper.Snap(dialog, "06-apply-preview", ModuleDir);

						SubmitAndWaitClose(dialog, "应用 stash（apply，保留条目）");

						// 真实仓库断言：s2.txt 内容恢复（--index 连暂存态一并恢复）；条目仍 2 条
						Assert.Equal("stashed 2\n",
							File.ReadAllText(Path.Combine(repo, "s2.txt")).Replace("\r\n", "\n"));
						Assert.Equal(2, StashSubjectsOf(repo).Length);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.ApplyStash_DeleteAfterApply = savedDeleteAfterApply;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 4) 应用 stash（pop 删除条目） ============================

		[Fact]
		public void ApplyStash_PopDeletesEntry()
		{
			string repo = TestRepoFactory.CreateStash();
			bool savedDeleteAfterApply = ForkPlusSettings.Default.ApplyStash_DeleteAfterApply;
			try
			{
				ForkPlusSettings.Default.ApplyStash_DeleteAfterApply = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						StashRevision top = StashNamed(WaitForStashes(repoControl, 2), "stash@{0}");
						Assert.Equal("stash-two", top.Message);

						var dialog = new ApplyStashWindow(repoControl, top);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();

						// 勾选"应用后删除" → pop 预览 + 警告图标
						UiClick.Toggle(dialog.DeleteStashAfterApplyCheckBox, true);
						Assert.Equal("git stash pop stash@{0}", CommandPreviewOf(dialog));
						Assert.True(dialog.DeleteStashWarningImage.IsVisible, "pop 模式应显示警告图标");
						ScreenshotHelper.Snap(dialog, "07-apply-pop", ModuleDir);

						SubmitAndWaitClose(dialog, "应用 stash（pop，删除条目）");

						// 真实仓库断言：内容恢复 + 条目只剩 stash-one（subject 带 "On main:" 前缀）
					Assert.Equal("stashed 2\n",
						File.ReadAllText(Path.Combine(repo, "s2.txt")).Replace("\r\n", "\n"));
					Assert.Equal(new[] { "On main: stash-one" }, StashSubjectsOf(repo));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.ApplyStash_DeleteAfterApply = savedDeleteAfterApply;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 5) 删除 stash（单模式） ============================

		[Fact]
		public void RemoveStash_SingleMode_DropsOne()
		{
			string repo = TestRepoFactory.CreateStash();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						StashRevision second = StashNamed(WaitForStashes(repoControl, 2), "stash@{1}");
						Assert.Equal("stash-one", second.Message);

						var dialog = new RemoveStashWindow(repoControl, new[] { second });
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 单模式：GitPointView 单视图装配，列表容器折叠（构造器按数量分流）
						Assert.Same(second, dialog.GitPointView.Value);
						Assert.True(dialog.GitPointView.IsVisible, "单模式应显示单视图");
						Assert.False(dialog.GitPointsContainer.IsVisible, "单模式应折叠列表容器");
						Assert.Equal(E2eMainWindowHarness.Tr("Delete"),
							UiClick.ContentText(footer.SubmitButton));
						Assert.True(footer.SubmitButton.IsEnabled, "删除 stash 提交应启用");
						Assert.Equal("git stash drop stash@{1}", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "08-remove-single", ModuleDir);

						SubmitAndWaitClose(dialog, "删除 stash@{1}");

						// 真实仓库断言：仅剩 stash-two（RemoveStashGitCommand → git stash drop；
					// subject 带 "On main:" 前缀）
					Assert.Equal(new[] { "On main: stash-two" }, StashSubjectsOf(repo));
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

		// ============================ 6) 删除 stash（多模式） ============================

		[Fact]
		public void RemoveStash_MultipleMode_DropsAll()
		{
			string repo = TestRepoFactory.CreateStash();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						StashRevision[] both = WaitForStashes(repoControl, 2).Items;
						Assert.Equal(2, both.Length);

						var dialog = new RemoveStashWindow(repoControl, both);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 多模式：列表装配，单视图折叠；按钮标题 "Delete 2 stashes"
						Assert.False(dialog.GitPointView.IsVisible, "多模式应折叠单视图");
						Assert.True(dialog.GitPointsContainer.IsVisible, "多模式应显示列表容器");
						Assert.Same(both, dialog.GitPoints.ItemsSource);
						Assert.Equal(E2eMainWindowHarness.TrFormat("Delete {0} stashes", 2),
							UiClick.ContentText(footer.SubmitButton));
						Assert.True(footer.SubmitButton.IsEnabled, "删除多 stash 提交应启用");
						string preview = CommandPreviewOf(dialog);
						Assert.Contains("git stash drop stash@{0}", preview);
						Assert.Contains("git stash drop stash@{1}", preview);
						ScreenshotHelper.Snap(dialog, "09-remove-multiple", ModuleDir);

						SubmitAndWaitClose(dialog, "删除 2 条 stash");

						// 真实仓库断言：批量 drop 清空（执行按 reflog 降序逐条 drop，防索引漂移）
						Assert.Empty(StashSubjectsOf(repo));
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

		// ============================ 7) 重命名 stash ============================

		[Fact]
		public void RenameStash_UpdatesMessage()
		{
			string repo = TestRepoFactory.CreateStash();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						StashRevision top = StashNamed(WaitForStashes(repoControl, 2), "stash@{0}");
						Assert.Equal("stash-two", top.Message);
						string oldSha = TestRepoFactory.GitOutput(repo, "rev-parse stash@{0}").Trim();

						var dialog = new RenameStashWindow(repoControl, top);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 预填当前消息；同值（无变化）禁用提交（IsSubmitAllowed）
						Assert.Equal("stash-two", dialog.StashNameTextBox.Text);
						Assert.False(footer.SubmitButton.IsEnabled, "消息未变化应禁用提交");

						// 新消息（含空格 → 预览加引号）
						dialog.StashNameTextBox.Text = "renamed stash two";
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "新消息应启用提交");
						Assert.Equal("git stash rename stash@{0} \"renamed stash two\"", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "10-rename", ModuleDir);

						SubmitAndWaitClose(dialog, "重命名 stash@{0}");

						// 真实仓库断言：新消息入栈、条目数不变（store 新 + drop 旧）、
					// 栈顶 sha = OutResultSha（RenameStashGitCommand 返回的新提交）。
					// 重命名条目经 git stash store -m 存入 → subject 为裸消息不带前缀；
					// 未动的 stash-one 保持 "On main: " 前缀（stash push 的标准格式）
					string[] subjects = StashSubjectsOf(repo);
					Assert.Equal(2, subjects.Length);
					Assert.Equal("renamed stash two", subjects[0]);
					Assert.Equal("On main: stash-one", subjects[1]);
						Assert.True(dialog.OutResultSha.HasValue, "OutResultSha 应有值");
						string newSha = TestRepoFactory.GitOutput(repo, "rev-parse stash@{0}").Trim();
						Assert.Equal(dialog.OutResultSha.Value.ToString(), newSha);
						Assert.NotEqual(oldSha, newSha);
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
