// E2E 模块13（2026-09-05）：历史改写 7 窗口（9 用例）。
// 覆盖：合并（干净合并真实执行 + 冲突预检警告/取消路径）/变基（预览 + autostash 开关 + 真实变基）/
// 拣选（单提交选项预览 + 多提交列表模式，真实执行）/还原（选项预览 + 真实 revert 提交）/
// 重置（soft/mixed/hard 三态预览 + 真实 hard reset）/Reflog（条目装配 + 模态泵确认跳转 =
// 真实 reset --hard）/交互式变基（真实 RI 辅助进程全链路：git rebase -i → ForkPlus.RI 经 IPC
// 回传 todo 列表 → 行内 ComboBox 生产路径 Drop → 提交 → rebase 落盘）。
// 模式：E2eMainWindowHarness.OpenRepository（真实 MainWindow 生产入口）→ 生产构造器建弹窗 →
// 控件树交互（UiClick.Click/Toggle + ComboBox.SelectedItem 走生产 SelectionChanged 管线）→
// WaitFor 弹窗关闭（AddUndoable/JobQueue 后台命令完成 → Close）→ TestRepoFactory.GitOutput
// 真实 git 状态断言。截图走 2026-09-06 口径：主窗口 1920×1080，弹窗按自然比例。
//
// 时序口径（模块 11/12 教训沿用）：
// - 本模块全部弹窗（Merge/Rebase/CherryPick/Revert/Reset/InteractiveRebase）均为
//   AddUndoable 型"命令完成才关"——SubmitAndWaitClose 直接适用；
// - ReflogWindow 是 CustomWindow（非 ForkPlusDialogWindow），Jump 不关窗——终态用
//   rev-parse 轮询；确认框走 MessageBoxWindow.ShowDialog DispatcherFrame 模态泵
//   （模块 5 同款：先 Post 泵内处理器再点触发按钮）。
//
// 命令预览口径：RebaseBranchWindow 的 destination 是 Reference，IGitPoint.ObjectName
// = FullReference → 预览 `git rebase refs/heads/main`（非裸分支名）；CherryPick/Revert 的
// sha 用 Revision.Sha.ToAbbreviatedString()（与窗口同一 API，避免缩写长度口径漂移）。
//
// RI 辅助进程（交互式变基）：RebaseInteractiveGitCommand 以 sequence.editor=AppContext.
// BaseDirectory/ForkPlus.RI 起 `git rebase -i`，RI 经命名管道（含本进程 PID）回传
// "prepareTodoListForRebase <todo文件>"；沙箱内若 DOTNET_ROOT 未设置，RI 的 apphost
// 解析不到 .NET 运行时（"You must install .NET to run this application"，探针实证）——
// 测试前 EnsureDotnetRootForRiHelper 从当前运行时目录推导并注入进程环境（git→RI 继承）。
using System;
using System.Collections.Generic;
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
	public class E2e13HistoryRewritingTests
	{
		private const string ModuleDir = "13-historyrewrite";

		// ============================ 共享助手 ============================

		/// <summary>等引用装配完成且双分支就位（后台 git 读取经 Dispatcher 回 UI）。</summary>
		private static RepositoryReferences WaitForRefs(RepositoryUserControl control, int minBranches = 2)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return control.RepositoryData != null
					&& control.RepositoryData.References.LocalBranches.Length >= minBranches
					&& control.RepositoryData.References.ActiveBranch != null
					&& control.RepositoryStatus != null;
			}), "引用/活跃分支/工作区状态未装配（15s 超时）");
			return control.RepositoryData.References;
		}

		private static LocalBranch BranchNamed(RepositoryReferences references, string name)
		{
			return references.LocalBranches.First(b => b.Name == name);
		}

		/// <summary>分支提交主题（顶→底，按行）。语言无关的纯 git 事实断言。</summary>
		private static string[] SubjectsOf(string repo, string refspec)
		{
			return TestRepoFactory.GitOutput(repo, "log --format=%s " + refspec)
				.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.ToArray();
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

		/// <summary>点提交并等弹窗关闭（AddUndoable 后台命令完成 → Close(result)）。</summary>
		private static void SubmitAndWaitClose(ForkPlusDialogWindow dialog, string what)
		{
			ForkPlusDialogFooter footer = FooterOf(dialog);
			UiClick.Click(footer.SubmitButton);
			Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
				what + "应在命令完成后关闭弹窗（15s 超时）");
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>按 sha 取 Revision（生产命令 GetRevisionsGitCommand——CherryPickWindow
		/// 构造期解析合并提交父代用的同一命令；bt_get_revision_headers 真实管线）。</summary>
		private static Revision RevisionFor(GitModule gitModule, string sha)
		{
			Assert.True(Sha.TryParse(sha, out Sha parsed), "sha 应可解析: " + sha);
			GitCommandResult<Revision[]> result = new GetRevisionsGitCommand().Execute(gitModule, new Sha[] { parsed });
			Assert.True(result.Succeeded, "GetRevisionsGitCommand 应成功: " + (result.Error?.FriendlyDescription ?? ""));
			Assert.Single(result.Result);
			return result.Result[0];
		}

		/// <summary>RI 辅助进程环境保障：ForkPlus.RI（git 的 sequence.editor 子进程）继承本进程
		/// 环境；DOTNET_ROOT 缺失时其 apphost 找不到 .NET 运行时（沙箱探针实证）。从当前
		/// 运行时目录（…/shared/Microsoft.NETCore.App/&lt;ver&gt;/）向上三级推导 .NET 根。</summary>
		private static void EnsureDotnetRootForRiHelper()
		{
			if (Environment.GetEnvironmentVariable("DOTNET_ROOT") != null)
			{
				return;
			}
			string runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
			string candidate = Path.GetFullPath(Path.Combine(runtimeDir, "..", "..", ".."));
			if (File.Exists(Path.Combine(candidate, "dotnet")))
			{
				Environment.SetEnvironmentVariable("DOTNET_ROOT", candidate);
			}
		}

		// ============================ 1) 合并：干净合并 ============================

		[Fact]
		public void MergeBranch_CleanMerge_CreatesMergeCommit()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			MergeType savedType = ForkPlusSettings.Default.MergeType;
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences refs = WaitForRefs(repoControl);
						LocalBranch feature = BranchNamed(refs, "feature");
						LocalBranch main = refs.ActiveBranch;
						Assert.Equal("main", main.Name);

						var dialog = new MergeBranchWindow(repoControl, feature, main);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 构造期 merge-tree 预检：无冲突
						Assert.Equal(E2eMainWindowHarness.Tr("Merge can be done without conflicts"),
							footer.StatusMessageTextBlock.Text);
						Assert.True(footer.SubmitButton.IsEnabled, "干净合并应启用提交");
						// 默认类型（Fast-forward if possible）→ 裸 git merge
						Assert.Equal("git merge feature", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "01-merge-default-preview", ModuleDir);

						// 切 No Fast-Forward → 预览加 --no-ff（SelectionChanged → RefreshCommandPreview）
						MergeBranchWindow.MergeOptionComboBoxItem noFF = dialog.MergeTypeComboBox.ItemsSource
							.OfType<MergeBranchWindow.MergeOptionComboBoxItem>()
							.First(i => i.Title == "No Fast-Forward");
						dialog.MergeTypeComboBox.SelectedItem = noFF;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git merge --no-ff feature", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "02-merge-no-ff-preview", ModuleDir);

						SubmitAndWaitClose(dialog, "合并 feature → main");

						// 真实仓库断言：合并提交 + 双亲 + 全部文件就位（git log 默认按提交日期序，
						// base two 晚于 f3 → [merge, base two, f3, f2, f1, base one]；仅断言
						// subjects[0]=合并提交（最新必在顶）+ 长度 + 包含集，顺序无关）
						Assert.Equal("Merge branch 'feature'",
							TestRepoFactory.GitOutput(repo, "log --format=%s -1 main").Trim());
						string parentsLine = TestRepoFactory.GitOutput(repo, "rev-list --parents -n 1 main").Trim();
						Assert.True(parentsLine.Trim().Split(' ').Length == 3, "合并提交应有双亲: " + parentsLine);
						string[] subjects = SubjectsOf(repo, "main");
						Assert.Equal(6, subjects.Length);
						Assert.Equal("Merge branch 'feature'", subjects[0]);
						// git log 默认按提交日期序（base two 晚于 f3 → [merge, base two, f3, f2, f1,
						// base one]）；除 subjects[0]=合并提交（最新必在顶）外断言包含集，顺序无关
						Assert.Contains("feat: one", subjects);
						Assert.Contains("feat: two", subjects);
						Assert.Contains("feat: three", subjects);
						Assert.Contains("base one", subjects);
						Assert.Contains("base two", subjects);
						foreach (string f in new[] { "base.txt", "b.txt", "f1.txt", "f2.txt", "f3.txt" })
						{
							Assert.True(File.Exists(Path.Combine(repo, f)), f + " 应存在于工作区");
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
				ForkPlusSettings.Default.MergeType = savedType;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 2) 合并：冲突预检 + 取消 ============================

		[Fact]
		public void MergeBranch_ConflictPreview_WarnsAndCancelKeepsRepo()
		{
			string repo = TestRepoFactory.CreateHistoryConflict();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences refs = WaitForRefs(repoControl);

						var dialog = new MergeBranchWindow(repoControl, BranchNamed(refs, "feature"), refs.ActiveBranch);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 构造期 merge-tree 预检：警告（两侧改同一行）
						Assert.Equal(E2eMainWindowHarness.Tr("Merge will cause conflicts"),
							footer.StatusMessageTextBlock.Text);
						Assert.True(footer.StatusImage.IsVisible, "冲突预检应显示警告图标");
						Assert.True(footer.StatusMessageTextBlock.IsVisible, "冲突预检消息应可见");
						Assert.True(footer.SubmitButton.IsEnabled, "冲突预检不禁用提交（git 自会报冲突并中止）");
						Assert.Equal("git merge feature", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "03-merge-conflict-warning", ModuleDir);

						// 取消路径：仓库零变更
						UiClick.Click(footer.CancelButton);
						Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
							"取消应关闭合并弹窗");
						string[] subjects = SubjectsOf(repo, "main");
						Assert.Equal(2, subjects.Length);
						Assert.Equal("main change", subjects[0]);
						Assert.Equal("", TestRepoFactory.GitOutput(repo, "diff --name-only HEAD").Trim());
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 3) 变基 ============================

		[Fact]
		public void RebaseBranch_CleanRebase_ReplaysCommitsOntoDestination()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			bool savedAutostash = ForkPlusSettings.Default.RebaseAutostash;
			bool savedUpdateRefs = ForkPlusSettings.Default.RebaseUpdateRefs;
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences refs = WaitForRefs(repoControl);
						LocalBranch feature = refs.ActiveBranch;
						Assert.Equal("feature", feature.Name);
						LocalBranch main = BranchNamed(refs, "main");

						var dialog = new RebaseBranchWindow(repoControl, feature, main);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 构造期 rebase 预检（merge-tree 演算）：无冲突
						Assert.Equal(E2eMainWindowHarness.Tr("Rebase can be done without conflicts"),
							footer.StatusMessageTextBlock.Text);
						Assert.True(footer.SubmitButton.IsEnabled, "干净变基应启用提交");
						// destination 是 Reference → ObjectName=FullReference → refs/heads/main
						Assert.Equal("git rebase refs/heads/main", CommandPreviewOf(dialog));
						// feature 区间内无其它本地分支 → update-refs 开关隐藏
						Assert.False(dialog.UpdateRefsCheckBox.IsVisible, "无依赖分支时 update-refs 开关应隐藏");
						ScreenshotHelper.Snap(dialog, "04-rebase-preview", ModuleDir);

						// autostash 开关 → 预览加 --autostash；随后关掉（工作区干净，无需 stash）
						UiClick.Toggle(dialog.AutostashCheckBox, true);
						Assert.Equal("git rebase --autostash refs/heads/main", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "05-rebase-autostash-preview", ModuleDir);
						UiClick.Toggle(dialog.AutostashCheckBox, false);
						Assert.Equal("git rebase refs/heads/main", CommandPreviewOf(dialog));

						SubmitAndWaitClose(dialog, "变基 feature onto main");

						// 真实仓库断言：feature 重放 3 个提交到 main 顶（线性，无合并提交）
						string[] subjects = SubjectsOf(repo, "feature");
						Assert.Equal(new[] { "feat: three", "feat: two", "feat: one", "base two", "base one" }, subjects);
						Assert.True(
							TestRepoFactory.GitOutput(repo, "rev-parse main").Trim()
								== TestRepoFactory.GitOutput(repo, "merge-base feature main").Trim(),
							"变基后 main 应为 feature 的祖先（merge-base = main）");
						// 工作区带上了 main 的 b.txt，且 f1-f3 仍在
						foreach (string f in new[] { "base.txt", "b.txt", "f1.txt", "f2.txt", "f3.txt" })
						{
							Assert.True(File.Exists(Path.Combine(repo, f)), f + " 应存在于工作区");
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
				ForkPlusSettings.Default.RebaseAutostash = savedAutostash;
				ForkPlusSettings.Default.RebaseUpdateRefs = savedUpdateRefs;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 4) 拣选：单提交（选项预览） ============================

		[Fact]
		public void CherryPick_SingleCommit_OptionsPreviewAndApply()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			bool savedAppend = ForkPlusSettings.Default.CherryPick_AppendOriginSha;
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences refs = WaitForRefs(repoControl);
						string f2Sha = TestRepoFactory.GitOutput(repo, "rev-parse feature~1").Trim();
						string f1Sha = TestRepoFactory.GitOutput(repo, "rev-parse feature~2").Trim();
						Revision f2 = RevisionFor(repoControl.GitModule, f2Sha);

						// 单提交模式（f2 非合并 → 父代数组单元素，与生产 DecoratedRevision.GetParents 等价）
						var dialog = new CherryPickWindow(repoControl, new Revision[] { f2 }, new Sha[] { ParseSha(f1Sha) });
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						Assert.Same(f2, dialog.RevisionGitPointView.Value);
						Assert.False(dialog.GitPointsContainer.IsVisible, "单提交模式应折叠列表容器");
						Assert.True(footer.SubmitButton.IsEnabled, "单提交拣选应启用提交");
						string abbrev = f2.Sha.ToAbbreviatedString();
						Assert.Equal("git cherry-pick " + abbrev, CommandPreviewOf(dialog));
						// 构造期预检：f2 加新文件 f2.txt，与 main 无重叠 → 干净
						Assert.Equal(E2eMainWindowHarness.Tr("Cherry-pick can be done without conflicts"),
							footer.StatusMessageTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "06-cherry-pick-preview", ModuleDir);

						// --no-commit：CommitCheckBox 复选框（生产 IsCheckedChanged 管线）
						UiClick.Toggle(dialog.CommitCheckBox, false);
						Assert.Equal("git cherry-pick --no-commit " + abbrev, CommandPreviewOf(dialog));
						Assert.False(dialog.AppendOriginShaCheckBox.IsEnabled, "no-commit 时 -x 追加开关应禁用");
						ScreenshotHelper.Snap(dialog, "07-cherry-pick-no-commit", ModuleDir);
						UiClick.Toggle(dialog.CommitCheckBox, true);
						Assert.Equal("git cherry-pick " + abbrev, CommandPreviewOf(dialog));

						// -x：追加 origin sha 开关
						UiClick.Toggle(dialog.AppendOriginShaCheckBox, true);
						Assert.Equal("git cherry-pick -x " + abbrev, CommandPreviewOf(dialog));
						UiClick.Toggle(dialog.AppendOriginShaCheckBox, false);

						SubmitAndWaitClose(dialog, "拣选 f2 → main");

						// 真实仓库断言：main 顶多一个 feat: two 提交，f2.txt 落地，f1/f3 不动
						string[] subjects = SubjectsOf(repo, "main");
						Assert.Equal(new[] { "feat: two", "base two", "base one" }, subjects);
						Assert.True(File.Exists(Path.Combine(repo, "f2.txt")), "f2.txt 应被拣选到工作区");
						Assert.False(File.Exists(Path.Combine(repo, "f1.txt")), "f1.txt 不应出现");
						Assert.False(File.Exists(Path.Combine(repo, "f3.txt")), "f3.txt 不应出现");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.CherryPick_AppendOriginSha = savedAppend;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 5) 拣选：多提交（列表模式） ============================

		[Fact]
		public void CherryPick_MultipleCommits_ListModeAndApply()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences refs = WaitForRefs(repoControl);
						string f2Sha = TestRepoFactory.GitOutput(repo, "rev-parse feature~1").Trim();
						string f3Sha = TestRepoFactory.GitOutput(repo, "rev-parse feature").Trim();
						Revision f2 = RevisionFor(repoControl.GitModule, f2Sha);
						Revision f3 = RevisionFor(repoControl.GitModule, f3Sha);

						// 多提交模式：生产入口按修订列表行序（新→旧）传参，窗口内部反转成旧→新
						var dialog = new CherryPickWindow(repoControl, new Revision[] { f3, f2 },
							new Sha[] { ParseSha(f2Sha) });
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 列表装配 2 项 + 单视图折叠（与单提交模式互补）
						Revision[] listed = dialog.GitPoints.ItemsSource.OfType<Revision>().ToArray();
						Assert.Equal(2, listed.Length);
						Assert.Contains(listed, r => r.Sha == f2.Sha);
						Assert.Contains(listed, r => r.Sha == f3.Sha);
						Assert.False(dialog.RevisionGitPointView.IsVisible, "多提交模式应折叠单视图");
						Assert.True(footer.SubmitButton.IsEnabled, "多提交拣选应启用提交");
						// 预览反转：旧→新（f2 在前）
						Assert.Equal("git cherry-pick " + f2.Sha.ToAbbreviatedString() + " " + f3.Sha.ToAbbreviatedString(),
							CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "08-cherry-pick-multi", ModuleDir);

						SubmitAndWaitClose(dialog, "拣选 f2+f3 → main");

						// 真实仓库断言：两提交按旧→新顺序重放到 main 顶
						string[] subjects = SubjectsOf(repo, "main");
						Assert.Equal(new[] { "feat: three", "feat: two", "base two", "base one" }, subjects);
						Assert.True(File.Exists(Path.Combine(repo, "f2.txt")));
						Assert.True(File.Exists(Path.Combine(repo, "f3.txt")));
						Assert.False(File.Exists(Path.Combine(repo, "f1.txt")));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 6) 还原 ============================

		[Fact]
		public void Revert_CreatesRevertCommit()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForRefs(repoControl);
						string baseTwoSha = TestRepoFactory.GitOutput(repo, "rev-parse main").Trim();
						string baseOneSha = TestRepoFactory.GitOutput(repo, "rev-parse main~1").Trim();
						Revision baseTwo = RevisionFor(repoControl.GitModule, baseTwoSha);

						var dialog = new RevertRevisionWindow(repoControl, baseTwo, new Sha[] { ParseSha(baseOneSha) });
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						Assert.Same(baseTwo, dialog.RevisionGitPointView.Value);
						Assert.True(footer.SubmitButton.IsEnabled, "还原应启用提交");
						string abbrev = baseTwo.Sha.ToAbbreviatedString();
						Assert.Equal("git revert " + abbrev, CommandPreviewOf(dialog));
						// 构造期预检：还原 HEAD 提交（b.txt 新增）→ 干净
						Assert.Equal(E2eMainWindowHarness.Tr("Revert can be done without conflicts"),
							footer.StatusMessageTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "09-revert-preview", ModuleDir);

						// --no-commit 预览态，随后恢复勾选（真实提交还原）
						UiClick.Toggle(dialog.CommitCheckBox, false);
						Assert.Equal("git revert --no-commit " + abbrev, CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "10-revert-no-commit", ModuleDir);
						UiClick.Toggle(dialog.CommitCheckBox, true);
						Assert.Equal("git revert " + abbrev, CommandPreviewOf(dialog));

						SubmitAndWaitClose(dialog, "还原 base two");

						// 真实仓库断言：revert 提交在顶 + b.txt 消失
						string[] subjects = SubjectsOf(repo, "main");
						Assert.Equal(3, subjects.Length);
						Assert.Equal("Revert \"base two\"", subjects[0]);
						Assert.False(File.Exists(Path.Combine(repo, "b.txt")), "b.txt 应被还原移除");
						Assert.True(File.Exists(Path.Combine(repo, "base.txt")));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 7) 重置 ============================

		[Fact]
		public void ResetBranch_TypePreviewAndHardReset()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences refs = WaitForRefs(repoControl);
						LocalBranch main = refs.ActiveBranch;
						Assert.Equal("main", main.Name);
						string baseOneSha = TestRepoFactory.GitOutput(repo, "rev-parse main~1").Trim();
						Revision baseOne = RevisionFor(repoControl.GitModule, baseOneSha);

						var dialog = new ResetBranchWindow(repoControl, main, baseOne);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						Assert.Same(main, dialog.ActiveBranchGitPointView.Value);
						Assert.True(footer.SubmitButton.IsEnabled, "重置应启用提交");
						string abbrev = baseOne.Sha.ToAbbreviatedString();

						// 三态预览：axaml 默认选中 Mixed（SelectedIndex=1）——构造器编程式选中修复后
						// 关闭态即生效（迁移回归修复：Avalonia ComboBox 容器延迟物化，IsSelected="True"
						// 在首次展开下拉前不生效，WPF 加载即生成容器故原版正常）
						Assert.Equal(1, dialog.ResetTypeCombobox.SelectedIndex);
						Assert.Equal("git reset --mixed " + abbrev, CommandPreviewOf(dialog));
						// Soft（Index 0，快捷键 S 同款 SelectionChanged 管线）
						dialog.ResetTypeCombobox.SelectedIndex = 0;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git reset --soft " + abbrev, CommandPreviewOf(dialog));
						// Hard（Index 2）
						dialog.ResetTypeCombobox.SelectedIndex = 2;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git reset --hard " + abbrev, CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "11-reset-hard-preview", ModuleDir);

						SubmitAndWaitClose(dialog, "硬重置 main → base one");

						// 真实仓库断言：main 指回 base one，b.txt 连工作区一起丢弃
						Assert.Equal(baseOneSha, TestRepoFactory.GitOutput(repo, "rev-parse main").Trim());
						Assert.Single(SubjectsOf(repo, "main"));
						Assert.False(File.Exists(Path.Combine(repo, "b.txt")), "硬重置应丢弃 b.txt");
						Assert.True(File.Exists(Path.Combine(repo, "base.txt")));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 8) Reflog：列表 + 跳转 ============================

		[Fact]
		public void Reflog_ListEntriesAndJumpResetsHead()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForRefs(repoControl);

						var reflog = new ReflogWindow(repoControl);
						reflog.Show();
						Dispatcher.UIThread.RunJobs();

						// 条目装配（构造即 LoadReflog；join undo-index 未命中 → reflog 原生 subject）
						Assert.True(UiClick.WaitFor(delegate
						{
							return reflog.ReflogListView.ItemsSource != null
								&& reflog.ReflogListView.ItemsSource.OfType<ReflogViewItem>().Count() > 0;
						}), "reflog 条目应装配（15s 超时）");
						List<ReflogViewItem> items = reflog.ReflogListView.ItemsSource.OfType<ReflogViewItem>().ToList();
						Assert.True(items.Count >= 6, "8 步历史至少应有 6 条 reflog（实际 " + items.Count + "）");
						// 状态栏条目数（本地化格式键 "{0} entries loaded."）
						Assert.Equal(E2eMainWindowHarness.TrFormat("{0} entries loaded.", items.Count), reflog.StatusText.Text);
						Assert.False(reflog.JumpButton.IsEnabled, "未选中条目时 Jump 应禁用");
						ScreenshotHelper.Snap(reflog, "12-reflog-window", ModuleDir);

						// 选中第 3 新条目（新→旧：[0]=commit base two、[1]=checkout main、[2]=commit f3）
						ReflogViewItem target = items[2];
						reflog.ReflogListView.SelectedItem = target;
						Dispatcher.UIThread.RunJobs();
						Assert.True(reflog.JumpButton.IsEnabled, "选中条目后 Jump 应启用");
						string expectedSha = target.Sha;

						// 模态确认泵（模块 5 同款）：先 Post 泵内处理器，再点 Jump 触发 ShowDialog
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
									handlerError[0] = "跳转确认框未出现";
									return;
								}
								ScreenshotHelper.Snap(msgBox, "13-reflog-jump-confirm", ModuleDir);
								string jumpTitle = E2eMainWindowHarness.Tr("Jump");
								Button jump = UiClick.FindAll<Button>(msgBox)
									.FirstOrDefault(delegate (Button b) { return UiClick.ContentText(b) == jumpTitle; });
								if (jump == null)
								{
									handlerError[0] = "确认框中找不到按钮 " + jumpTitle;
									return;
								}
								jump.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
								handled[0] = true;
							}
							catch (Exception ex)
							{
								handlerError[0] = ex.ToString();
							}
						}, DispatcherPriority.Background);

						UiClick.Click(reflog.JumpButton); // → JumpToSelected → MessageBox.ShowDialog 模态泵

						Assert.True(handled[0], "模态确认框处理器未执行：" + handlerError[0]);
						Assert.Null(handlerError[0]);

						// AddUndoable 后台 reset --hard：轮询终态（ReflogWindow 不自动关）
						Assert.True(UiClick.WaitFor(delegate
						{
							return TestRepoFactory.GitOutput(repo, "rev-parse HEAD").Trim() == expectedSha;
						}), "跳转应把 HEAD reset --hard 到所选 reflog 状态（15s 超时）");
						Assert.True(
							TestRepoFactory.GitOutput(repo, "rev-parse main").Trim() == expectedSha,
							"reset --hard 应移动当前分支 main（实际 " + TestRepoFactory.GitOutput(repo, "rev-parse main").Trim() + "）");
						// 工作区 = f3 提交状态：f1-f3/base 在、b.txt 无
						foreach (string f in new[] { "base.txt", "f1.txt", "f2.txt", "f3.txt" })
						{
							Assert.True(File.Exists(Path.Combine(repo, f)), f + " 应存在于跳转后工作区");
						}
						Assert.False(File.Exists(Path.Combine(repo, "b.txt")), "b.txt 不应在 f3 状态工作区");

						// Refresh 按钮：跳转后重读 reflog（新 reset 条目入列）
						UiClick.Click(reflog.RefreshButton);
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return reflog.ReflogListView.ItemsSource != null
								&& reflog.ReflogListView.ItemsSource.OfType<ReflogViewItem>().Count() > items.Count;
						}), "刷新后应看到 reset 产生的新 reflog 条目");
						ScreenshotHelper.Snap(reflog, "14-reflog-after-jump", ModuleDir);

						reflog.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ 9) 交互式变基（真实 RI 辅助进程全链路） ============================

		[Fact]
		public void InteractiveRebase_RealRiFlow_DropsCommit()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			bool savedUpdateRefs = ForkPlusSettings.Default.InteractiveRebase_UpdateRefs;
			bool savedBackup = ForkPlusSettings.Default.InteractiveRebase_CreateBackup;
			try
			{
				EnsureDotnetRootForRiHelper();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					InteractiveRebaseWindow dialog = null;
					try
					{
						RepositoryReferences refs = WaitForRefs(repoControl);
						LocalBranch feature = refs.ActiveBranch;
						Assert.Equal("feature", feature.Name);
						LocalBranch main = BranchNamed(refs, "main");

						// 构造即启动真实 `git rebase -i`（sequence.editor=ForkPlus.RI，
						// RI 经 IPC 回传 git-rebase-todo → 窗口解析装配 todo 列表）
						dialog = new InteractiveRebaseWindow(repoControl, repoControl.GitModule, feature, main, null);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();

						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.RevisionListView.ItemsSource != null
								&& dialog.RevisionListView.ItemsSource.OfType<RevisionEntry>().Count() == 3;
						}), "todo 列表应装配 3 个提交（RI→IPC→GetRebaseTodoListCommand，15s 超时）");
						RevisionEntry[] entries = dialog.RevisionListView.ItemsSource.OfType<RevisionEntry>().ToArray();
						Assert.Contains(entries, e => e.Subject == "feat: one");
						Assert.Contains(entries, e => e.Subject == "feat: two");
						Assert.Contains(entries, e => e.Subject == "feat: three");
						Assert.All(entries, e => Assert.Equal(InteractiveRebaseAction.Pick, e.Action));

						// 命令预览：git rebase -i main（FriendlyName）
						Assert.Equal("git rebase -i main", CommandPreviewOf(dialog));
						ForkPlusDialogFooter footer = FooterOf(dialog);
						Assert.True(footer.SubmitButton.IsEnabled, "todo 就位后提交应启用");
						ScreenshotHelper.Snap(dialog, "15-ir-todolist-loaded", ModuleDir);

						// 行内 ComboBox 生产路径选 Drop：容器内的 ActionsComboBox
						// SelectionChanged → entry.Action=Drop + UpdateTodoList
						RevisionEntry f2Entry = entries.First(e => e.Subject == "feat: two");
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.RevisionListView.ContainerFromItem(f2Entry) != null;
						}), "feat: two 行容器应实现（渲染后）");
						InteractiveRebaseComboBoxItem dropItem = dialog.InteractiveRebaseComboBoxItemsSource
							.First(i => i.Title == "Drop");
						ComboBox actionsCombo = dialog.RevisionListView.ContainerFromItem(f2Entry)
							.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault();
						Assert.NotNull(actionsCombo);
						actionsCombo.SelectedItem = dropItem;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(InteractiveRebaseAction.Drop, f2Entry.Action);
						ScreenshotHelper.Snap(dialog, "16-ir-drop-selected", ModuleDir);

						// 提交 → OnSubmit 写回 todo 文件 → RI 放行 → git 完成 rebase → 窗口关闭
						SubmitAndWaitClose(dialog, "交互式变基（Drop f2）");

						// 真实仓库断言：变基目标为 main 顶端（base two），feature 重放 f1+f3（f2 被丢弃）
						string[] subjects = SubjectsOf(repo, "feature");
						Assert.Equal(new[] { "feat: three", "feat: one", "base two", "base one" }, subjects);
						Assert.False(File.Exists(Path.Combine(repo, "f2.txt")), "被 Drop 的 f2.txt 不应存在");
						Assert.True(File.Exists(Path.Combine(repo, "f1.txt")));
						Assert.True(File.Exists(Path.Combine(repo, "f3.txt")));
						Assert.True(File.Exists(Path.Combine(repo, "b.txt")), "重放到 main 顶端，b.txt 应在场");
						Assert.True(
							TestRepoFactory.GitOutput(repo, "rev-parse main").Trim()
								== TestRepoFactory.GitOutput(repo, "merge-base feature main").Trim(),
							"变基后 main 应为 feature 的祖先（merge-base = main）");
					}
					finally
					{
						try
						{
							if (dialog != null)
							{
								if (dialog.IsVisible)
								{
									// 失败兜底：走生产取消路径停掉 RI/git（绕过 OnClosing 的
									// 确认框，避免收尾模态泵挂死测试会话）
									typeof(InteractiveRebaseWindow).GetMethod("StopRebaseInteractiveProcess",
										System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
										?.Invoke(dialog, new object[] { "cancel" });
									UiClick.WaitFor(delegate { return !dialog.IsVisible; }, 5000);
								}
								dialog.Dispose();
							}
						}
						catch
						{
							// 兜底尽力而为，不掩盖断言
						}
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.InteractiveRebase_UpdateRefs = savedUpdateRefs;
				ForkPlusSettings.Default.InteractiveRebase_CreateBackup = savedBackup;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void InteractiveRebase_RealRiFlow_RewordAppliesMessage()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			bool savedUpdateRefs = ForkPlusSettings.Default.InteractiveRebase_UpdateRefs;
			bool savedBackup = ForkPlusSettings.Default.InteractiveRebase_CreateBackup;
			try
			{
				EnsureDotnetRootForRiHelper();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					InteractiveRebaseWindow dialog = null;
					try
					{
						RepositoryReferences refs = WaitForRefs(repoControl);
						LocalBranch feature = refs.ActiveBranch;
						LocalBranch main = BranchNamed(refs, "main");

						// 真实 `git rebase -i` 全链路（sequence.editor=ForkPlus.RI → IPC → todo 装配）
						dialog = new InteractiveRebaseWindow(repoControl, repoControl.GitModule, feature, main, null);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();

						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.RevisionListView.ItemsSource != null
								&& dialog.RevisionListView.ItemsSource.OfType<RevisionEntry>().Count() == 3;
						}), "todo 列表应装配 3 个提交（15s 超时）");
						RevisionEntry[] entries = dialog.RevisionListView.ItemsSource.OfType<RevisionEntry>().ToArray();
						RevisionEntry f2Entry = entries.First(e => e.Subject == "feat: two");
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.RevisionListView.ContainerFromItem(f2Entry) != null;
						}), "feat: two 行容器应实现（渲染后）");

						// 生产路径等效：Reword 弹窗 OK 后置 Action=Reword + CustomMessage（含正文）
						f2Entry.Action = InteractiveRebaseAction.Reword;
						f2Entry.CustomMessage = "feat: two reworded by e2e" + "\n\n" + "reworded body line";
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("feat: two reworded by e2e", f2Entry.Subject);

						// 提交 → OnSubmit 写 todo + fork-message-archive → git 执行 reword 指令时
						// core.editor（ForkPlus.RI）→ IPC → TryApplyArchivedMessage 改写 COMMIT_EDITMSG
						SubmitAndWaitClose(dialog, "交互式变基（Reword f2）");

						// 真实仓库断言：f2 主题与正文都应变为改写后的消息
						string[] subjects = SubjectsOf(repo, "feature");
						Assert.Equal(new[] { "feat: three", "feat: two reworded by e2e", "feat: one", "base two", "base one" }, subjects);
						string fullBody = TestRepoFactory.GitOutput(repo, "log --format=%B -1 feature~1").Trim();
						Assert.StartsWith("feat: two reworded by e2e", fullBody);
						Assert.Contains("reworded body line", fullBody);
					}
					finally
					{
						try
						{
							if (dialog != null)
							{
								if (dialog.IsVisible)
								{
									typeof(InteractiveRebaseWindow).GetMethod("StopRebaseInteractiveProcess",
										System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
										?.Invoke(dialog, new object[] { "cancel" });
									UiClick.WaitFor(delegate { return !dialog.IsVisible; }, 5000);
								}
								dialog.Dispose();
							}
						}
						catch
						{
							// 兜底尽力而为，不掩盖断言
						}
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.InteractiveRebase_UpdateRefs = savedUpdateRefs;
				ForkPlusSettings.Default.InteractiveRebase_CreateBackup = savedBackup;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		private static Sha ParseSha(string sha)
		{
			Assert.True(Sha.TryParse(sha, out Sha parsed), "sha 应可解析: " + sha);
			return parsed;
		}
	}
}
