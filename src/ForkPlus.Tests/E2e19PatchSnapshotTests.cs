// E2E 模块19（2026-09-06）：补丁与快照 3 窗口 + 4 命令链（7 用例）。
// 覆盖：SaveAsPatchWindow（单修订 Revision:/范围 Revisions: 标签 + revisions 列表装配
// （GetRevisionsInRangeGitCommand 真实执行）+ 命令预览三态（无目录/裸目录/含空格目录引号）
// + OnSubmit 系统保存对话框取消降级路径）/ApplyPatchWindow（文件路径构造器：预填 +
// 非 From 头 CreateCommitsCheckBox 折叠 + `git apply <path>` 预览（空格路径加引号）+
// 不存在路径提交禁用 + apply --check 冲突检测双态（Success/Warning 状态条）+ 真实 apply：
// 文件落工作区不建提交；剪贴板 byte[] 构造器：Location/Path/Browse 三件套折叠 + From 头
// CreateCommitsCheckBox 显示 + `git am`↔`git apply` 预览切换 + 真实 am：补丁提交入历史）/
// SaveSnapshotWindow（预览三态 + 真实快照：stash create + store 入栈且**工作区完整保留**
// （快照与模块14 SaveStash 清空工作区的语义分野）+ stageNewFiles 先暂存新文件再入栈）/
// ExportPatchGitCommand（生产命令对象直测：单修订 --max-count=1 单 patch / 范围 src~..dst
// 双 patch 序号）/SnapshotGitCommand + RestoreSnapshotGitCommand（UndoEntry 生命周期：
// 抓快照（headSha/branch/stashSha 三字段）→ 切分支 + reset --hard + 删文件破坏现场 →
// checkout + reset --hard + stash apply --index 全恢复）。
//
// 模式：窗口用例走 E2eMainWindowHarness.OpenRepository（真实 MainWindow 生产入口，弹窗
// OnSubmit 走 repoControl.JobQueue）；纯命令用例（Export/SnapshotRestore）GitModule 直构
// 免开主窗。截图 2026-09-06 口径：主窗口 1920×1080，弹窗按自然比例。
//
// 探针实证（2026-09-06，本模块环境闭环）：
// ① headless 的 TopLevel.StorageProvider 是 Avalonia.Platform.Storage.NoopStorageProvider
//    ——SaveFilePickerAsync 立即 RanToCompletion 返回 null（不挂起），生产同步桥接
//    StorageProviderDialogs.ShowSaveDialog 返回 false。因此 SaveAsPatchWindow.OnSubmit 的
//    系统保存对话框在 headless 下=用户点"取消"：SelectPatchSaveLocation false → 直接
//    Close() 无导出。真实导出链走生产同一命令对象 ExportPatchGitCommand 直测（窗口
//    OnSubmit 内部调用的就是它）；取消降级路径本身作为用例断言（窗口关闭 + 零文件 +
//    RecentPatchDirectory 不被改写）。
// ② git stash create [<message>] 的 --include-untracked 会被当 message 吞掉（stash create
//    子命令不支持该选项，只有 push/save 支持）——subject 变 "On main: --include-untracked"
//    且 untracked 文件不入快照。WPF 原仓 SnapshotGitCommand 逐字节同款（原始行为非迁移
//    回归，按原版语义断言并注明）；SaveWorkingDirectoryAsStashGitCommand 的 stash create
//    <msg> 恒加 "On main: " 前缀（与 push 相同），stash store 保持 subject。
// ③ SaveAsPatchWindow 预览的 range 方向是 `_src.._dst`（新..旧），而实际执行 ExportPatch
//    用 `dst~..src`（旧父之后到新）——预览是展示语义且与 WPF 原仓逐字节一致（原版行为，
//    按原版语义断言）。
// ④ git format-patch 范围输出的序号按旧→新排（与 log 逆序相反）：f2~..f3 → [PATCH 1/2]=f2、
//    [PATCH 2/2]=f3。
// ⑤ git apply --3way 隐含 --index（git 文档原文 "--3way implies the --index option"）——
//    新文件补丁落地后入暂存区（"A "）而非未跟踪（"??"），且不建提交。
// ⑥ GitOf 助手的全局 .Trim() 会吃掉 porcelain 首行的前导空格（" M a.txt" → "M a.txt"）——
//    XY 状态码断言必须逐行 Split 后比较。
//
// 本模块修复的两个迁移期生产 bug（探针实锋试出，修复已入生产代码）：
// A. SaveAsPatchWindow：WPF 原仓在 OnInitialized override 里加载 revisions，WPF 的
//    Initialized 在构造完成后触发（字段已就绪）；Avalonia 12 的 Initialized 在 TopLevel
//    基类构造链中触发（PresentationSource..ctor → OnAttachedToVisualTreeCore →
//    InitializeIfNeeded），此刻派生类构造器字段全未赋值 → _gitModule=null → GitRequest NRE
//    → 列表永远装配不上（生产必现）。修复：加载挪到构造器尾部（LoadRevisions）。
// B. ApplyPatchWindow 剪贴板模式：PlaceholderTextBox 初始化异步派发一次空 TextChanged，
//    把 _patchContainsCommitHeader 重置为 false → From 头补丁的 Create commit 勾选框在
//    弹窗打开后消失（生产必现）。修复：剪贴板模式（_patchData != null）忽略路径文本事件。
//    （FileHistoryWindow.OnInitialized 存在同类问题 A，属模块20 范围，届时处理。）
//
// 时序口径（模块 11-18 教训沿用）：ApplyPatch/SaveSnapshot 均为 JobQueue 型"命令完成才关"
// （SubmitAndWaitClose 直接适用）；SaveAsPatch 的 OnSubmit 同步直返（无命令入队）。
// 设置污染防护（模块 7/12/14 教训）：RecentPatchDirectory / SaveStash_StageNewFiles 被
// 预览与 OnSubmit 读写——用例开头快照 + 显式归零保证确定性初态，finally 恢复 + Save()。
// TextChanged 异步派发（模块18 根因闭环）：PathTextBox/StashMessageTextBox 的程序化
// .Text 赋值后必须 Dispatcher.UIThread.RunJobs() 泵一次再断言。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Undo;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e19PatchSnapshotTests
	{
		private const string ModuleDir = "19-patchsnapshot";

		// ============================ 共享助手 ============================

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

		/// <summary>点提交并等弹窗关闭（JobQueue 后台命令完成 → Dispatcher.Post(Close(result))）。</summary>
		private static void SubmitAndWaitClose(ForkPlusDialogWindow dialog, string what)
		{
			ForkPlusDialogFooter footer = FooterOf(dialog);
			UiClick.Click(footer.SubmitButton);
			Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
				what + "应在命令完成后关闭弹窗（15s 超时）");
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>生产同源 Revision 对象（GetRevisionsInRangeGitCommand 单修订查询——窗口
		/// OnInitialized 装配列表用的同一命令，比手工 new Revision 更贴近入口链）。</summary>
		private static Revision RevisionOf(GitModule gitModule, string sha)
		{
			Assert.True(Sha.TryParse(sha, out Sha parsed), "sha 应可解析: " + sha);
			GitCommandResult<GetRevisionsInRangeGitCommand.Result> result =
				new GetRevisionsInRangeGitCommand().Execute(gitModule, parsed, null);
			Assert.True(result.Succeeded, "GetRevisionsInRange 应成功: " + (result.Error?.FriendlyDescription ?? ""));
			Assert.Single(result.Result.Revisions);
			return result.Result.Revisions[0];
		}

		/// <summary>等 SaveAsPatchWindow 的 OnInitialized 异步装配（Task.Run 加载 → Dispatcher 回 UI）。</summary>
		private static void WaitForRevisions(SaveAsPatchWindow dialog, int expectedCount)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return dialog.RevisionsItemsControl.ItemsSource is Revision[] revisions && revisions.Length == expectedCount;
			}), "revisions 列表应装配 " + expectedCount + " 条（15s 超时）");
		}

		private static string GitOf(string repo, string args)
		{
			return TestRepoFactory.GitOutput(repo, args).Trim();
		}

		// ============================ 1) SaveAsPatch 单修订 ============================

		[Fact]
		public void SaveAsPatch_SingleRevision_PreviewAndCancelDegradation()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			string savedPatchDir = ForkPlusSettings.Default.RecentPatchDirectory;
			try
			{
				ForkPlusSettings.Default.RecentPatchDirectory = null; // 确定性初态（模块7/12 教训）
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						GitModule gitModule = repoControl.GitModule;
						string f3 = GitOf(repo, "rev-parse HEAD"); // feature: f1→f2→f3

						// —— 实例 A：无 RecentPatchDirectory 的裸预览 ——
						var dialog = new SaveAsPatchWindow(repoControl, gitModule, RevisionOf(gitModule, f3), null);
						dialog.Show();
						WaitForRevisions(dialog, 1);
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						Assert.Equal(E2eMainWindowHarness.Tr("Revision:"), dialog.RevisionsTextBlock.Text);
						Assert.Equal("git format-patch " + f3.Substring(0, 7), CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "01-save-as-patch-single", ModuleDir);

						// —— OnSubmit 降级路径：headless NoopStorageProvider = 用户在系统保存
						// 对话框点"取消"（探针①）→ 窗口关闭、零导出、RecentPatchDirectory 不被改写
						UiClick.Click(footer.SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
							"保存对话框取消后应关闭弹窗（15s 超时）");
						Assert.Null(ForkPlusSettings.Default.RecentPatchDirectory);
						Assert.Equal("", GitOf(repo, "status --porcelain"));

						// —— 实例 B：RecentPatchDirectory 预置（含空格目录 → 引号；预览在
						// OnInitialized 加载完成的 RefreshCommandPreview 读设置）——
						ForkPlusSettings.Default.RecentPatchDirectory = "/tmp/fpe2e out dir";
						var dialogB = new SaveAsPatchWindow(repoControl, gitModule, RevisionOf(gitModule, f3), null);
						dialogB.Show();
						WaitForRevisions(dialogB, 1);
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("git format-patch " + f3.Substring(0, 7) + " -o \"/tmp/fpe2e out dir\"",
							CommandPreviewOf(dialogB));
						ScreenshotHelper.Snap(dialogB, "02-save-as-patch-output-dir", ModuleDir);
						dialogB.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.RecentPatchDirectory = savedPatchDir;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 2) SaveAsPatch 范围模式 ============================

		[Fact]
		public void SaveAsPatch_RangeMode_PreviewAndRevisionsList()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			string savedPatchDir = ForkPlusSettings.Default.RecentPatchDirectory;
			try
			{
				ForkPlusSettings.Default.RecentPatchDirectory = null;
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						GitModule gitModule = repoControl.GitModule;
						string f3 = GitOf(repo, "rev-parse HEAD");
						string f2 = GitOf(repo, "rev-parse HEAD~1");
						Assert.True(Sha.TryParse(f2, out Sha dstSha), "f2 sha 应可解析");

						// 入口语义（ShowSaveRevisionsAsPatchWindowCommand）：多选 2 修订 →
						// revisions[0]=主（新端 src）、revisions[1]=次（旧端 dst）
						var dialog = new SaveAsPatchWindow(repoControl, gitModule, RevisionOf(gitModule, f3), dstSha);
						dialog.Show();
						// 范围 = log f2~..f3 = f1..f3（排除 f1）→ [f3, f2] 两条（log 逆序）
						WaitForRevisions(dialog, 2);
						Dispatcher.UIThread.RunJobs();

						Assert.Equal(E2eMainWindowHarness.Tr("Revisions:"), dialog.RevisionsTextBlock.Text);
						Revision[] revisions = (Revision[])dialog.RevisionsItemsControl.ItemsSource;
						Assert.Equal(new[] { "feat: three", "feat: two" },
							revisions.Select(r => r.Message).ToArray());

						// 预览方向 src..dst（新..旧）——与 WPF 原仓逐字节一致（探针③，原版行为）
						Assert.Equal("git format-patch " + f3.Substring(0, 7) + ".." + f2.Substring(0, 7),
							CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "03-save-as-patch-range", ModuleDir);
						dialog.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.RecentPatchDirectory = savedPatchDir;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 3) ExportPatch 真实命令链 ============================

		[Fact]
		public void ExportPatch_SingleAndRangeRealCommand()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			string outDir = Path.Combine(Path.GetTempPath(), "fpe2e_patchexport_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			try
			{
				// 纯命令链：GitModule 直构（模块18 Track 同款），窗口 OnSubmit 内部调用的同一命令对象
				var gitModule = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
				Directory.CreateDirectory(outDir);
				string f3 = GitOf(repo, "rev-parse HEAD");
				string f2 = GitOf(repo, "rev-parse HEAD~1");
				Assert.True(Sha.TryParse(f3, out Sha dst), "f3 sha 应可解析");
				Assert.True(Sha.TryParse(f2, out Sha src), "f2 sha 应可解析");

				// 单修订：format-patch --max-count=1 <sha> → 单 patch（From 头 + Subject + diff）
				string singlePath = Path.Combine(outDir, "single.patch");
				GitCommandResult single = new ExportPatchGitCommand().Execute(gitModule, dst, null, singlePath);
				Assert.True(single.Succeeded, "单修订导出应成功: " + (single.Error?.FriendlyDescription ?? ""));
				string singlePatch = File.ReadAllText(singlePath);
				Assert.StartsWith("From " + f3, singlePatch);
				Assert.Contains("Subject: [PATCH] feat: three", singlePatch);
				Assert.Contains("f3.txt", singlePatch);

				// 范围：format-patch f2~..f3 → 两个 patch（f2 + f3）带 1/2、2/2 序号。
			// git format-patch 序号按旧→新排（与 log 逆序相反）：1/2=f2、2/2=f3——探针实证
			string rangePath = Path.Combine(outDir, "range.patch");
			GitCommandResult range = new ExportPatchGitCommand().Execute(gitModule, dst, src, rangePath);
			Assert.True(range.Succeeded, "范围导出应成功: " + (range.Error?.FriendlyDescription ?? ""));
			string rangePatch = File.ReadAllText(rangePath);
			Assert.Equal(2, Regex.Matches(rangePatch, "^From ", RegexOptions.Multiline).Count);
			Assert.Contains("Subject: [PATCH 1/2] feat: two", rangePatch);
			Assert.Contains("Subject: [PATCH 2/2] feat: three", rangePatch);
			Assert.Contains("f2.txt", rangePatch); // f2 的 diff 内容在（f1 排除在 f2~..f3 范围外）
			Assert.Contains("f3.txt", rangePatch);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
				if (Directory.Exists(outDir))
				{
					Directory.Delete(outDir, recursive: true);
				}
			}
		}

		// ============================ 4) ApplyPatch 文件路径 ============================

		[Fact]
		public void ApplyPatch_FilePath_PreviewConflictAndRealApply()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: false); // main: base one → base two，干净
			string outDir = Path.Combine(Path.GetTempPath(), "fpe2e apply dir " + Guid.NewGuid().ToString("N").Substring(0, 6));
			try
			{
				// 生成补丁：新增文件 new.txt（git add -N + git diff —— intent-to-add 让新文件进 diff）
				string patchPath = Path.Combine(outDir, "new-file.patch");
				Directory.CreateDirectory(outDir);
				File.WriteAllText(Path.Combine(repo, "new.txt"), "patched line\n");
				TestRepoFactory.GitOutput(repo, "add -N new.txt");
				File.WriteAllText(patchPath, TestRepoFactory.GitOutput(repo, "diff"));
				// 还原现场（清 intent-to-add 索引条目 + 删文件 → 干净基线供 apply）
				TestRepoFactory.GitOutput(repo, "reset -q");
				File.Delete(Path.Combine(repo, "new.txt"));
				Assert.Equal("", GitOf(repo, "status --porcelain"));

				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// —— 干净基线：Success 状态 + git apply 预览（含空格路径加引号）——
						var dialog = new ApplyPatchWindow(repoControl, patchPath);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						Assert.Equal(patchPath, dialog.PathTextBox.Text);
						Assert.True(footer.SubmitButton.IsEnabled, "存在文件时提交应启用");
						// git diff 输出以 "diff --git" 开头（非 "From " 头）→ Create commit 折叠
						Assert.False(dialog.CreateCommitsCheckBox.IsVisible, "非 format-patch 补丁不应显示 Create commit");
						Assert.Equal("git apply \"" + patchPath + "\"", CommandPreviewOf(dialog));
						Assert.Equal(E2eMainWindowHarness.Tr("Patch can be applied without conflicts"),
							footer.StatusMessageTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "04-apply-patch-file", ModuleDir);

						// —— 不存在路径：提交禁用（TextChanged 异步派发，须泵——模块18 教训）——
					dialog.PathTextBox.Text = "/nonexistent/x.patch";
					Dispatcher.UIThread.RunJobs();
					Assert.False(footer.SubmitButton.IsEnabled, "不存在路径提交应禁用");
					// 原版语义（WPF 原仓 GetCommandPreview 逐字节一致）：非空路径即预览完整命令，
					// 不校验存在性——存在性由提交按钮禁用表达（空路径才折叠预览）
					Assert.Equal("git apply /nonexistent/x.patch", CommandPreviewOf(dialog));

						// —— 冲突检测：new.txt 已存在（内容不同）→ apply --check 失败 → Warning ——
						File.WriteAllText(Path.Combine(repo, "new.txt"), "conflicting existing\n");
						dialog.PathTextBox.Text = patchPath;
						Dispatcher.UIThread.RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled, "存在文件时提交应启用（冲突仅警告不禁用——原版行为）");
						Assert.Equal(E2eMainWindowHarness.Tr("Patch will cause conflicts"),
							footer.StatusMessageTextBlock.Text);
						ScreenshotHelper.Snap(dialog, "05-apply-patch-conflict", ModuleDir);
						dialog.Close();
						File.Delete(Path.Combine(repo, "new.txt")); // 还原干净基线

						// —— 真实 apply：JobQueue → apply --3way → Close(result)，文件落工作区不建提交 ——
					var applyDialog = new ApplyPatchWindow(repoControl, patchPath);
					applyDialog.Show();
					Dispatcher.UIThread.RunJobs();
					Assert.True(UiClick.WaitFor(delegate
					{
						return FooterOf(applyDialog).StatusMessageTextBlock.Text
							== E2eMainWindowHarness.Tr("Patch can be applied without conflicts");
					}), "干净基线应回到无冲突状态（15s 超时）");
					SubmitAndWaitClose(applyDialog, "应用补丁");
					Assert.True(applyDialog.GitResult.Succeeded,
						"apply 应成功: " + (applyDialog.GitResult.Error?.FriendlyDescription ?? ""));
					Assert.Equal("patched line\n",
						File.ReadAllText(Path.Combine(repo, "new.txt")).Replace("\r\n", "\n"));
					// git apply --3way 隐含 --index（git 文档："--3way implies the --index option"，
					// 原仓同旗标）——落地文件入暂存区；但 apply 不建提交（HEAD 不动）
					string[] applyStatus = TestRepoFactory.GitOutput(repo, "status --porcelain")
						.Split('\n', StringSplitOptions.RemoveEmptyEntries);
					Assert.Equal(new[] { "A  new.txt" }, applyStatus);
					Assert.Equal("base two", GitOf(repo, "log -1 --format=%s")); // 不建提交
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
				if (Directory.Exists(outDir))
				{
					Directory.Delete(outDir, recursive: true);
				}
			}
		}

		// ============================ 5) ApplyPatch 剪贴板（git am） ============================

		[Fact]
		public void ApplyPatch_FromClipboard_AmCreatesCommit()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: false); // main HEAD=base two，干净
			try
			{
				// 补丁源：本仓 feature~2 = f1（新增 f1.txt，基于 base one）→ format-patch "From " 头
				byte[] patchData = Encoding.UTF8.GetBytes(
					TestRepoFactory.GitOutput(repo, "format-patch -1 feature~2 --stdout"));
				Assert.True(Encoding.UTF8.GetString(patchData).StartsWith("From ", StringComparison.Ordinal),
					"format-patch 输出应以 From 头开头");

				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new ApplyPatchWindow(repoControl, patchData);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 剪贴板形态：Location/Path/Browse 三件套折叠（Collapse）
						Assert.False(dialog.LocationLabel.IsVisible, "剪贴板模式应隐藏 Location 标签");
						Assert.False(dialog.PathTextBox.IsVisible, "剪贴板模式应隐藏路径输入框");
						Assert.False(dialog.BrowseButton.IsVisible, "剪贴板模式应隐藏 Browse 按钮");
						// From 头 → Create commit 可见；默认未勾选 → git apply 预览（无路径）
						Assert.True(dialog.CreateCommitsCheckBox.IsVisible, "From 头补丁应显示 Create commit");
						Assert.True(footer.SubmitButton.IsEnabled, "剪贴板数据提交应恒启用");
						Assert.Equal("git apply", CommandPreviewOf(dialog));
						Assert.Equal(E2eMainWindowHarness.Tr("Patch can be applied without conflicts"),
							footer.StatusMessageTextBlock.Text);

						// 勾选 Create commit → git am（IsCheckedChanged 生产管线）
						UiClick.Toggle(dialog.CreateCommitsCheckBox, true);
						Assert.Equal("git am", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "06-apply-patch-clipboard", ModuleDir);

						// 真实 am：JobQueue → am --3way → Close(result) → 补丁提交入历史
						SubmitAndWaitClose(dialog, "应用补丁（am）");
						Assert.True(dialog.GitResult.Succeeded,
							"am 应成功: " + (dialog.GitResult.Error?.FriendlyDescription ?? ""));
						Assert.Equal("feat: one", GitOf(repo, "log -1 --format=%s"));
						Assert.True(File.Exists(Path.Combine(repo, "f1.txt")), "am 应落地补丁文件");
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

		// ============================ 6) SaveSnapshot 快照保留工作区 ============================

		[Fact]
		public void SaveSnapshot_PreviewAndRealSnapshotKeepsWorkingDirectory()
		{
			string repo = TestRepoFactory.CreateStashWork(); // a mod + b mod + c untracked
			bool savedStageNewFiles = ForkPlusSettings.Default.SaveStash_StageNewFiles;
			try
			{
				ForkPlusSettings.Default.SaveStash_StageNewFiles = false; // 确定性初态（模块14 教训）
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dialog = new SaveSnapshotWindow(repoControl);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 预览三态：裸 push → -m 消息 → --include-untracked（TextChanged/IsCheckedChanged 生产管线）
						Assert.True(footer.SubmitButton.IsEnabled, "快照提交应默认启用");
						Assert.Equal("git stash push", CommandPreviewOf(dialog));
						dialog.StashMessageTextBox.Text = "snap msg";
						Dispatcher.UIThread.RunJobs(); // TextChanged 异步派发须泵（模块18 教训）
						Assert.Equal("git stash push -m \"snap msg\"", CommandPreviewOf(dialog));
						UiClick.Toggle(dialog.StageNewFilesCheckBox, true);
						Assert.Equal("git stash push --include-untracked -m \"snap msg\"", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "07-save-snapshot", ModuleDir);

						// 真实快照：JobQueue → SaveWorkingDirectoryAsStashGitCommand（stash create + store）
						SubmitAndWaitClose(dialog, "保存快照");
						Assert.True(dialog.GitResult.Succeeded,
							"快照应成功: " + (dialog.GitResult.Error?.FriendlyDescription ?? ""));

						// stash 入栈 1 条（stash create 恒加 "On main: " 前缀 + store 保持——探针②）
						Assert.Equal(new[] { "On main: snap msg" }, StashSubjectsOf(repo));
						// 快照语义核心：工作区完整保留（与模块14 SaveStash 清空工作区分野）——
						// stash create 只建悬空 commit 不动工作区/index
						Assert.Equal("a modified\n", File.ReadAllText(Path.Combine(repo, "a.txt")).Replace("\r\n", "\n"));
						Assert.Equal("b modified\n", File.ReadAllText(Path.Combine(repo, "b.txt")).Replace("\r\n", "\n"));
						Assert.True(File.Exists(Path.Combine(repo, "c.txt")), "快照不应清工作区，c.txt 应保留");
						// stageNewFiles=true：c.txt 先 stage（入 stash）且 stage 状态保留。
					// porcelain 须逐行切分断言（GitOf 的全局 Trim 会吃掉首行 " M a.txt" 的
					// 前导空格——探针教训），逐行比较 XY 状态码不受影响
					string[] statusLines = TestRepoFactory.GitOutput(repo, "status --porcelain")
						.Split('\n', StringSplitOptions.RemoveEmptyEntries);
					Assert.Contains("A  c.txt", statusLines);
					Assert.Contains(" M a.txt", statusLines);
					Assert.Contains(" M b.txt", statusLines);
						// stash 内容：跟踪修改 a/b + 已暂存新文件 c（stash create 捕获 index）
						string stashFiles = GitOf(repo, "stash show --name-only stash@{0}");
						Assert.Contains("a.txt", stashFiles);
						Assert.Contains("b.txt", stashFiles);
						Assert.Contains("c.txt", stashFiles);
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

		/// <summary>stash 条目主题（栈顶→栈底）。语言无关的纯 git 事实断言。</summary>
		private static string[] StashSubjectsOf(string repo)
		{
			return TestRepoFactory.GitOutput(repo, "stash list --format=%s")
				.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.ToArray();
		}

		// ============================ 7) 快照抓取与恢复（UndoEntry 生命周期） ============================

		[Fact]
		public void SnapshotRestore_UndoEntryLifecycle()
		{
			string repo = TestRepoFactory.CreateStashWork(); // main（base a→base b），工作区：a mod + b mod + c untracked
			try
			{
				var gitModule = new GitModule(repo, Path.Combine(repo, ".git"), null, null);

				// —— 抓快照：headSha + 当前分支 + 工作区 stash sha（tracked 脏 → 非 null）——
				GitCommandResult<UndoEntry> snapshot = new SnapshotGitCommand().Execute(gitModule, "test snapshot");
				Assert.True(snapshot.Succeeded);
				UndoEntry entry = snapshot.Result;
				Assert.Equal(GitOf(repo, "rev-parse HEAD"), entry.HeadSha);
				Assert.Equal("main", entry.CurrentBranchName);
				Assert.NotNull(entry.PreOperationStashSha);
				// 原版语义（探针②，WPF 原仓 SnapshotGitCommand 逐字节同款，非迁移回归）：
				// stash create 不支持 --include-untracked（被吞为 message）——untracked 不入快照
				string snapshotFiles = GitOf(repo, "show --name-only --format= " + entry.PreOperationStashSha);
				Assert.Contains("a.txt", snapshotFiles);
				Assert.Contains("b.txt", snapshotFiles);
				Assert.DoesNotContain("c.txt", snapshotFiles);

				// —— 破坏现场：切新分支 + reset --hard 退一提交（b.txt 消失）+ 删 a.txt ——
				TestRepoFactory.GitOutput(repo, "checkout -q -b wreck");
				TestRepoFactory.GitOutput(repo, "reset -q --hard HEAD~1");
				Assert.False(File.Exists(Path.Combine(repo, "b.txt")));
				File.Delete(Path.Combine(repo, "a.txt"));

				// —— 恢复：checkout main + reset --hard entry.HeadSha + stash apply --index ——
				// 生产链路恒带 JobMonitor（undo 执行走 JobQueue；GitRequest.Execute 内部的
				// JobMonitorExtensions.Append 无空守卫，null 会 NRE）——测试同款直构
				GitCommandResult restore = new RestoreSnapshotGitCommand().Execute(gitModule, entry, new JobMonitor());
				Assert.True(restore.Succeeded, "快照恢复应成功: " + (restore.Error?.FriendlyDescription ?? ""));
				Assert.Equal("main", GitOf(repo, "symbolic-ref --short HEAD"));
				Assert.Equal(entry.HeadSha, GitOf(repo, "rev-parse HEAD"));
				Assert.Equal("a modified\n", File.ReadAllText(Path.Combine(repo, "a.txt")).Replace("\r\n", "\n"));
				Assert.Equal("b modified\n", File.ReadAllText(Path.Combine(repo, "b.txt")).Replace("\r\n", "\n"));
				// stash create 不入栈（悬空 commit），恢复后 stash list 仍空
				Assert.Equal("", GitOf(repo, "stash list"));
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
