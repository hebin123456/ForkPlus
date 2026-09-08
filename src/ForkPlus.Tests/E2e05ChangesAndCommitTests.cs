// E2E 模块5（2026-09-05）：变更与提交视图（Commit 视图）。
// 覆盖：真实 MainWindow 生产路径打开仓库（TabManager.OpenRepository，IsActiveRepository 管线走通）、
//   工作区状态装配（未暂存 修改/删除/未跟踪 + 已暂存）、选中文件 working dir diff 加载、
//   Stage/Unstage 选中文件（真实 git add / reset，UI 列表 + git index 双重验证）、
//   StageAllButton 智能切换（Stage All ↔ Unstage All）、提交按钮状态与文案、
//   行级 chunk stage/discard 浮窗（DiffSelectionLayer 悬浮按钮 → ApplyChunk/DiscardChunk 命令 →
//   git apply 真实执行 + 确认对话框模态泵驱动）、提交消息自动补全（Co-authored-by 建议 → Tab 选中替换）。
// 截图 → docs/evidence/e2e/05-changescommit/。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e05ChangesAndCommitTests
	{
		[Fact]
		public void CommitView_LoadsWorkingDirectoryStatus_AndDiffForSelectedFile()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// ===== 1) 切到 Commit 视图（生产公共入口）=====
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;

						// ===== 2) 状态装配：3 未暂存（a.txt 改 / b.txt 删 / new.txt 未跟踪）+ 1 已暂存（c.txt）=====
						// （IsActiveRepository 已走通：RepositoryStatusUpdated → RefreshRepositoryStatusUi → SetDataAsync）
						bool statusLoaded = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						});
						Assert.True(statusLoaded, "工作区状态未装配：unstaged=" + stage.AllUnstagedFiles.Length
							+ " staged=" + stage.AllStagedFiles.Length + "（15s 超时）");
						Assert.Equal(new[] { "a.txt", "b.txt", "new.txt" },
							stage.AllUnstagedFiles.Select(f => f.Path).OrderBy(p => p).ToArray());
						Assert.Equal(new[] { "c.txt" }, stage.AllStagedFiles.Select(f => f.Path).ToArray());
						// 变更类型（git status 真实解析结果）
						Assert.Equal(ChangeType.Modified, stage.AllUnstagedFiles.First(f => f.Path == "a.txt").ChangeType);
						Assert.Equal(ChangeType.Deleted, stage.AllUnstagedFiles.First(f => f.Path == "b.txt").ChangeType);
						Assert.Equal(ChangeType.Added, stage.AllUnstagedFiles.First(f => f.Path == "new.txt").ChangeType);
						Assert.False(stage.AllUnstagedFiles.First(f => f.Path == "new.txt").Tracked, "new.txt 应未跟踪");
						Assert.True(stage.AllStagedFiles[0].Staged, "c.txt 应带已暂存标记");

						// ===== 3) 自动选中首个未暂存文件 → working dir diff 加载（LoadWorkingDirectoryDiff 真实管线）=====
						Assert.True(stage.SelectedUnstagedFiles.Length == 1, "应自动选中首个未暂存文件，实际 "
							+ stage.SelectedUnstagedFiles.Length);
						bool diffLoaded = UiClick.WaitFor(delegate
						{
							return commit.FileDiffControl.Content != null && commit.FileDiffControl.Content.Succeeded;
						});
						Assert.True(diffLoaded, "选中文件后 working dir diff 应加载成功（15s 超时）");
						Assert.True(diffLoaded && commit.FileDiffControl.Content.Result is ParsedDiffContent,
							"文本文件 diff 应为 ParsedDiffContent，实际 "
							+ commit.FileDiffControl.Content.Result?.GetType().Name);
						Assert.Equal(stage.SelectedUnstagedFiles[0].Path,
							commit.FileDiffControl.Content.Result.ChangedFile.Path);

						// ===== 4) 提交按钮：已暂存 1 项 → "Commit 1 File"；无主题 → 禁用 =====
						Assert.Equal(E2eMainWindowHarness.TrFormat("Commit {0} File", 1), commit.CommitButton.Content?.ToString());
						Assert.False(commit.CommitButton.IsEnabled, "无提交主题时提交按钮应禁用");

						ScreenshotHelper.Snap(window, "01-commit-view-loaded", "05-changescommit");
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
		public void CommitView_StageAndUnstageSelectedFile_ViaToolbarButtons()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						}), "初始状态未装配");

						// ===== 1) 选中未暂存的 a.txt → 点 Stage → git add + 列表移动 =====
						stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						// 注：首个未暂存文件（a.txt）本就被自动选中，SelectFile 会重复 Add（生产怪癖），
						// 断言用去重路径集合而非数量
						Assert.Equal(new[] { "a.txt" }, stage.SelectedUnstagedFiles.Select(f => f.Path).Distinct().ToArray());
						Assert.True(stage.IsUnstagedListSelected, "未暂存列表应处于选中态");

						UiClick.Click(stage.StageButton); // Stage 事件 → StageSelectedFiles → ToggleFileStage → git add
						bool staged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 2 && stage.AllStagedFiles.Length == 2;
						});
						Assert.True(staged, "暂存 a.txt 后未暂存应剩 2 项、已暂存应为 2 项，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);
						Assert.Contains("a.txt", stage.AllStagedFiles.Select(f => f.Path));
						Assert.DoesNotContain("a.txt", stage.AllUnstagedFiles.Select(f => f.Path));
						// 真实 git index 二次验证（UI 断言之外的事实）
						Assert.Contains("a.txt", TestRepoFactory.GitOutput(repo, "diff --cached --name-only").Split('\n'));
						Assert.Equal(E2eMainWindowHarness.TrFormat("Commit {0} Files", 2), commit.CommitButton.Content?.ToString());
						ScreenshotHelper.Snap(window, "02-stage-selected-file", "05-changescommit");

						// ===== 2) 选中已暂存的 a.txt → 点 Unstage → git reset + 列表移回 =====
						stage.StagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(new[] { "a.txt" }, stage.SelectedStagedFiles.Select(f => f.Path).Distinct().ToArray());
						Assert.True(stage.IsStagedListSelected, "已暂存列表应处于选中态");

						UiClick.Click(stage.UnstageButton); // Unstage 事件 → UnstageSelectedFiles → git reset
						bool unstaged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						});
						Assert.True(unstaged, "取消暂存 a.txt 后应回到 3 未暂存 / 1 已暂存，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);
						Assert.Contains("a.txt", stage.AllUnstagedFiles.Select(f => f.Path));
						Assert.DoesNotContain("a.txt", TestRepoFactory.GitOutput(repo, "diff --cached --name-only").Split('\n'));
						ScreenshotHelper.Snap(window, "03-unstage-selected-file", "05-changescommit");
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
		public void CommitView_StageAllButton_SmartTogglesBetweenStageAllAndUnstageAll()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						}), "初始状态未装配");

						// ===== 1) 未暂存有可见项 → StageAllButton 表现为 Stage All：全部 4 项进入已暂存 =====
						UiClick.Click(stage.StageAllButton);
						bool allStaged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 0 && stage.AllStagedFiles.Length == 4;
						});
						Assert.True(allStaged, "Stage All 后未暂存应为空、已暂存应为 4 项，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);
						Assert.Equal(new[] { "a.txt", "b.txt", "c.txt", "new.txt" },
							stage.AllStagedFiles.Select(f => f.Path).OrderBy(p => p).ToArray());
						// index 二次验证（含删除 b.txt 与未跟踪 new.txt 的暂存）
						string cached = TestRepoFactory.GitOutput(repo, "diff --cached --name-only");
						Assert.Contains("a.txt", cached.Split('\n'));
						Assert.Contains("b.txt", cached.Split('\n'));
						Assert.Contains("new.txt", cached.Split('\n'));
						Assert.Equal(E2eMainWindowHarness.TrFormat("Commit {0} Files", 4), commit.CommitButton.Content?.ToString());
						ScreenshotHelper.Snap(window, "04-stage-all", "05-changescommit");

						// ===== 2) 未暂存已空 → 同一按钮智能切换为 Unstage All：全部 4 项回到未暂存 =====
						// （c.txt 取消暂存后仍相对 HEAD 有工作区改动 → 回到未暂存列表）
						UiClick.Click(stage.StageAllButton);
						bool allUnstaged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 4 && stage.AllStagedFiles.Length == 0;
						});
						Assert.True(allUnstaged, "Unstage All 后应为 4 未暂存 / 0 已暂存，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);
						Assert.Equal(new[] { "a.txt", "b.txt", "c.txt", "new.txt" },
							stage.AllUnstagedFiles.Select(f => f.Path).OrderBy(p => p).ToArray());
						// index 已空（c.txt 的既有暂存也被一并取消）
						Assert.Equal(string.Empty, TestRepoFactory.GitOutput(repo, "diff --cached --name-only"));
						Assert.Equal(E2eMainWindowHarness.Tr("Commit"), commit.CommitButton.Content?.ToString());
						ScreenshotHelper.Snap(window, "05-unstage-all", "05-changescommit");
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
		// ===== 模块5第二批共用助手 =====

		/// <summary>
		/// 等 Commit 视图状态装配完成（3 未暂存 + 1 已暂存）。
		/// 装配期间 <see cref="CommitUserControl.StageFileUserControl"/>（x:Name 字段，XAML 加载完成前）
		/// 或其内文件列表可为 null——WaitFor 条件必须空安全（条件里解引用 null 会直接 NRE 炸出
		/// WaitFor 而不是等到超时，2026-09-07 沙盒慢机实证；与 E2e05b 同款测试自身缺陷）。
		/// </summary>
		private static StageFileUserControl WaitForWorkingDirStatus(CommitUserControl commit)
		{
			StageFileUserControl stage = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				// 装配竞态防御：慢机（沙盒）上视图装配期间属性链可能瞬态抛 NRE
				// （2026-09-07 实证：同一用例两次运行 NRE 位置不同——commit 链 vs 文件列表链，
				// 都是装配中途快照）。捕获后视为"本轮未就绪"继续轮询；真装配完成则条件成立，
				// 真永久失败则超时走诊断消息，不再把装配瞬态当成测试崩溃。
				try
				{
					stage = commit.StageFileUserControl;
					return stage != null
						&& stage.UnstagedFilesFileListUserControl != null
						&& stage.StagedFilesFileListUserControl != null
						&& stage.AllUnstagedFiles.Length == 3
						&& stage.AllStagedFiles.Length == 1;
				}
				catch (NullReferenceException)
				{
					return false;
				}
			}), "初始状态未装配：stage=" + (stage == null ? "<null>" : "就绪")
				+ " unstaged=" + (stage == null ? "?" : stage.AllUnstagedFiles.Length.ToString())
				+ " staged=" + (stage == null ? "?" : stage.AllStagedFiles.Length.ToString()));
			return stage;
		}

		/// <summary>选中未暂存 a.txt 并等待 working dir diff 加载、编辑器进入可视树。</summary>
		private static CommitCodeEditor SelectFileAndLoadDiff(Window window, StageFileUserControl stage, CommitUserControl commit)
		{
			stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
			Dispatcher.UIThread.RunJobs();
			Assert.True(UiClick.WaitFor(delegate
			{
				return commit.FileDiffControl.Content != null && commit.FileDiffControl.Content.Succeeded;
			}), "a.txt 的 working dir diff 未加载");
			CommitCodeEditor editor = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				editor = UiClick.FindAll<CommitCodeEditor>(window).FirstOrDefault();
				return editor != null;
			}), "diff 编辑器（CommitCodeEditor）未出现在可视树");
			return editor;
		}

		/// <summary>程序化选区并强制渲染一帧，令选区浮窗（Stage/Discard 悬浮按钮）出现。</summary>
		private static void SelectLineAndShowFloatingButtons(Window window, CommitCodeEditor editor, string lineText)
		{
			int selStart = editor.Text.IndexOf(lineText, StringComparison.Ordinal);
			Assert.True(selStart >= 0, "diff 文档中找不到 " + lineText);
			editor.Select(selStart, (lineText + "\n").Length);
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			// 强制真实渲染一帧：Render → DrawSelectionBorder → ShowChunkAdorner（选区顶部出浮窗）
			HeadlessWindowExtensions.CaptureRenderedFrame(window);
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
		}

		private static FloatingButton FindFloatingButton(Window window, string content)
		{
			return UiClick.FindAll<FloatingButton>(window)
				.FirstOrDefault(delegate (FloatingButton b)
				{
					return UiClick.ContentText(b) == content;
				});
		}

		/// <summary>
		/// 轮询等待选区浮窗按钮出现（每轮强制渲染一帧：浮窗经 Render → DrawSelectionBorder →
		/// ShowChunkAdorner 链路出现）。立即断言在冷启动/慢机下假红：首个触发该路径的用例
		/// 必挂、后续同款全过（2026-09-07 沙盒实证，浮窗首用初始化时序）。
		/// </summary>
		private static FloatingButton WaitForFloatingButton(Window window, string content)
		{
			FloatingButton button = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				HeadlessWindowExtensions.CaptureRenderedFrame(window);
				button = FindFloatingButton(window, content);
				return button != null;
			}), "选区浮窗按钮未出现：" + content);
			return button;
		}

		[Fact]
		public void CommitView_LineLevelStage_FloatingButton_StagesOnlySelectedLines()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			// 固定 Split 布局（单编辑器、diff 文本含 +/- 标记）——行级选区逻辑按 Split 断言，
			// 防其他测试/历史运行遗留 SideBySide 用户设置（编辑器会变两个，FirstOrDefault 拿到
			// 左侧旧内容编辑器导致找不到 "+line4-appended"）。
			DiffLayoutMode originalLayout = ForkPlusSettings.Default.CommitDiffLayoutMode;
			try
			{
				ForkPlusSettings.Default.CommitDiffLayoutMode = DiffLayoutMode.Split;
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = WaitForWorkingDirStatus(commit);
						CommitCodeEditor editor = SelectFileAndLoadDiff(window, stage, commit);

						// ===== 1) 选中新增第一行（line4-appended）→ 浮窗出现 [Stage, Discard...] =====
						SelectLineAndShowFloatingButtons(window, editor, "line4-appended");
						FloatingButton stageBtn = WaitForFloatingButton(window, E2eMainWindowHarness.Tr("Stage"));
						FloatingButton discardBtn = WaitForFloatingButton(window, E2eMainWindowHarness.Tr("Discard..."));
						Assert.True(stageBtn != null, "未暂存 diff 选区浮窗应出现 Stage 按钮");
						Assert.True(discardBtn != null, "未暂存 diff 选区浮窗应出现 Discard... 按钮");
						ScreenshotHelper.Snap(window, "06-floating-buttons-on-selection", "05-changescommit");

						// ===== 2) 点浮窗 Stage → ApplyChunkCommand → git apply --cached（仅 line4 的部分补丁）=====
						UiClick.Click(stageBtn);

						// UI 刷新发生在 git apply + RefreshFileStatus 完成之后，作为 git 已执行的信号
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllStagedFiles.Any(delegate (ChangedFile f) { return f.Path == "a.txt"; });
						}), "部分 stage 后 a.txt 应出现在已暂存列表");

						// git index 事实断言（用 + 前缀匹配真实变更行，git diff 的 context 行也会带原文本）：
						// 仅 line4 进入暂存区，line5 仍是工作区改动
						string cached = TestRepoFactory.GitOutput(repo, "diff --cached -- a.txt");
						Assert.Contains("+line4-appended", cached);
						Assert.DoesNotContain("+line5-appended", cached);
						string worktree = TestRepoFactory.GitOutput(repo, "diff -- a.txt");
						Assert.DoesNotContain("+line4-appended", worktree);
						Assert.Contains("+line5-appended", worktree);

						// 部分暂存后 a.txt 两端都有差异 → 两侧列表同时存在
						Assert.Contains("a.txt", stage.AllUnstagedFiles.Select(f => f.Path));
						ScreenshotHelper.Snap(window, "07-line-level-stage-applied", "05-changescommit");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.CommitDiffLayoutMode = originalLayout;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void CommitView_LineLevelDiscard_FloatingButton_ConfirmDialog_DiscardsSelectedLines()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			// 固定 Split 布局（同上：防 SideBySide 用户设置残留干扰行级选区断言）。
			DiffLayoutMode originalLayout = ForkPlusSettings.Default.CommitDiffLayoutMode;
			try
			{
				ForkPlusSettings.Default.CommitDiffLayoutMode = DiffLayoutMode.Split;
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = WaitForWorkingDirStatus(commit);
						CommitCodeEditor editor = SelectFileAndLoadDiff(window, stage, commit);

						// ===== 1) 选中新增第二行（line5-appended）→ 浮窗出现 =====
						SelectLineAndShowFloatingButtons(window, editor, "line5-appended");
						FloatingButton discardBtn = WaitForFloatingButton(window, E2eMainWindowHarness.Tr("Discard..."));
						Assert.True(discardBtn != null, "未暂存 diff 选区浮窗应出现 Discard... 按钮");
						ScreenshotHelper.Snap(window, "08-discard-floating-button", "05-changescommit");

						// ===== 2) 模态确认框驱动：ShowDialog 走 DispatcherFrame 模态泵，
						// 先 Post 处理器（泵内执行：找确认框 → 截图 → 点确认），再点击 Discard 触发模态 =====
						var handled = new bool[1];
						var handlerError = new string[1];
						Dispatcher.UIThread.Post(delegate
						{
							try
							{
								ForkPlus.UI.Dialogs.MessageBoxWindow msgBox = ForkPlus.UI.WpfCompat.WpfApp.Windows
									.OfType<ForkPlus.UI.Dialogs.MessageBoxWindow>()
									.FirstOrDefault();
								if (msgBox == null)
								{
									handlerError[0] = "丢弃确认框未出现";
									return;
								}
								// 证据：确认框（标题/描述/按钮）
								ScreenshotHelper.Snap(msgBox, "09-discard-confirm-dialog", "05-changescommit");
								// 单行丢弃 → 提交按钮文案 "Discard Line"（DiscardChunkCommand 单复数分支）
								string submitTitle = E2eMainWindowHarness.Tr("Discard Line");
								Button submit = UiClick.FindAll<Button>(msgBox)
									.FirstOrDefault(delegate (Button b) { return UiClick.ContentText(b) == submitTitle; });
								if (submit == null)
								{
									handlerError[0] = "确认框中找不到按钮 " + submitTitle;
									return;
								}
								submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
								handled[0] = true;
							}
							catch (Exception ex)
							{
								handlerError[0] = ex.ToString();
							}
						}, DispatcherPriority.Background);

						UiClick.Click(discardBtn); // → ExtractPatchAndApply(Discard) → MessageBoxWindow.ShowDialog → 模态泵

						Assert.True(handled[0], "模态确认框处理器未执行：" + handlerError[0]);
						Assert.Null(handlerError[0]);

						// ===== 3) git 工作区事实断言：line5 被反向补丁丢弃，line4 保留 =====
						string expectedContent = "line1\nline2\nline3\nline4-appended\n";
						Assert.True(UiClick.WaitFor(delegate
						{
							return File.ReadAllText(Path.Combine(repo, "a.txt")) == expectedContent;
						}), "line5 应被丢弃（ApplyWorkingTreeGitCommand 反向补丁）");
						string worktree = TestRepoFactory.GitOutput(repo, "diff -- a.txt");
						Assert.DoesNotContain("line5-appended", worktree);
						Assert.Contains("line4-appended", worktree);

						ScreenshotHelper.Snap(window, "10-line-level-discard-applied", "05-changescommit");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.CommitDiffLayoutMode = originalLayout;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void CommitView_CommitMessageAutocomplete_SuggestsCoAuthoredBy()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						WaitForWorkingDirStatus(commit);

						// ===== 1) 消息输入：先设主题（FullCommitMessage setter 会以 DisableUpdates 静默写 description，
						// 不触发建议），再对描述框直接输入 "Co-auth"（走 OnTextChanged → 30ms 防抖建议刷新）=====
						commit.FullCommitMessage = "write something\n";
						AutoCompleteTextBox desc = commit.CommitDescriptionTextBox;
						Dispatcher.UIThread.RunJobs();
						desc.Text = "Co-auth";
						// 防抖 30ms 窗口内把 caret 放末尾（RefreshSuggestions 回调执行时读取当前 CaretIndex）
						desc.CaretIndex = desc.Text.Length;

						// ===== 2) 建议（DelayedAction 30ms → Dispatcher.Post → OpenPopup）=====
						bool popupOpened = UiClick.WaitFor(delegate
						{
							Popup popup = UiClick.TryFind<Popup>(desc, "Popup");
							return popup != null && popup.IsOpen == true;
						});
						Assert.True(popupOpened, "输入 Co-auth 后补全建议浮层未弹出");
						Popup suggestionPopup = UiClick.TryFind<Popup>(desc, "Popup");
						// Popup 的内容挂在独立 PopupRoot（不在原窗口可视树），经 Child 直接取
						ListBox suggestionList = suggestionPopup.Child as ListBox;
						Assert.NotNull(suggestionList);
						// 防回归（2026-09-05 修复"建议浮层显示类名"）：ItemTemplate 必须是类型分发器，
						// 为 null 时 ListBox 按 ToString() 渲染出 AutoCompleteSuggestion 类名
						Assert.IsType<ForkPlus.UI.Controls.AutoCompleteSuggestionTemplateSelector>(suggestionList.ItemTemplate);
						Assert.Single(suggestionList.Items);
						Assert.Equal("Co-authored-by: ",
							(suggestionList.Items[0] as AutoCompleteSuggestion).Suggestion);
						ScreenshotHelper.Snap(window, "11-commit-message-autocomplete", "05-changescommit");

						// ===== 3) Tab 选中首条建议（fallbackToFirst）→ 替换 token、caret 定位、浮层关闭 =====
						desc.RaiseEvent(new KeyEventArgs
						{
							RoutedEvent = InputElement.KeyDownEvent,
							Key = Key.Tab
						});
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("Co-authored-by: ", desc.Text);
						Assert.Equal("Co-authored-by: ".Length, desc.CaretIndex);
						Assert.True(suggestionPopup.IsOpen != true, "选中建议后浮层应关闭");

						// FullCommitMessage 组合（getter 为 subject + "\n\n" + description）
						Assert.Equal("write something\n\nCo-authored-by: ", commit.FullCommitMessage);
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
