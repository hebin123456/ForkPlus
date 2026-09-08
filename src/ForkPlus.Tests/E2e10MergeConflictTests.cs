// E2E 模块10（2026-09-05）：合并冲突窗口（MergeConflictUserControl + SideBySideMergeWindow）。
// 覆盖：
//   1) Commit 视图选中 Unmerged 文件 → MergeConflictUserControl 装配（双 CheckBox 默认勾选、
//      ResolveButton 状态机：Merge / Choose {0} / 禁用 三态、our/theirs 侧 Git 点视图装配）
//   2) Choose theirs（主入口）：仅勾 Remote → Resolve → ResolveConflictGitCommand
//      （checkout-index --stage=3 + git add）→ 文件内容 + git index 双验证 + UI 列表迁移（M 暂存）
//   3) 三方编辑器（SideBySideMergeWindow，模态泵驱动）：Remote/Merged/Local 三编辑器视图装配、
//      MergeStatus 0/1 → 1/1、全选 ours（AllLocalCheckBox → SelectAll → IsResolved）→
//      Resolve 提交（ResolveMergeConflictGitCommand 写回 + git add）→ 内容无冲突标记；
//      全选 ours 解析内容 = HEAD → 无状态条目、文件从两个列表都消失（与用例 2 的暂存迁移互补）
//   4) 冲突块选择（多冲突块仓库）：行级选择事件（OnMergeLineAdded = MergeLineNumberMargin
//      点击的生产等价入口）按块逐侧解决（1/2 部分解决时 Resolve 禁用）→ 2/2 → 提交后
//      内容按块混合（block1 ours + block2 theirs）→ M 暂存 + UI 列表迁移
//   5) 滚动同步回归（大文件冲突仓库）：三编辑器垂直+水平同步（OnScrollOffsetChanged →
//      ScrollTo*OffsetCompat）、交替滚动防抖收敛（100ms 守卫，Sleep(150) 模拟换面板节奏）、
//      Next/Prev 冲突块导航（ScrollChunkIntoView → ScrollToLine）
// 截图 → docs/evidence/e2e/10-mergeconflict/。
// 测试经验（模块5/7 遗产沿用）：模态窗 ShowDialog 用 Post+DispatcherFrame 模态泵驱动；
//   涉及 ForkPlusSettings 的用例 finally 恢复 + Save() 落盘防污染（E2e07 教训）。
// 本轮新增教训：
//   ① 模态泵 handler 内断言失败 → 窗口不关 → DispatcherFrame 永不退出 → dotnet test 挂死
//     （CPU 0% 实证）——catch 必须强制 Close(false) 让错误以测试失败形式浮出；
//   ② 多冲突块仓库的块间公共行必须 ≥7（git 合并 marker size=7，探针实证 1 行分隔
//     只产生 1 个合并 hunk，"0/2" 断言直接失败并触发 ① 的死锁）；
//   ③ ours 解析 = HEAD 内容 → git status 无条目（无差异不显示，探针实证），
//     验证"移入已暂存列表"必须选 theirs（stage=3 内容 ≠ HEAD）。
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Merge;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e10MergeConflictTests
	{
		/// <summary>打开仓库切 Commit 视图，选中冲突文件并等 MergeConflictUserControl 装配。</summary>
		private static MergeConflictUserControl OpenCommitViewAndWaitConflict(
			string repo, string filePath, out MainWindow outWindow, out CommitUserControl outCommit)
		{
			RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out MainWindow window);
			outWindow = window;
			repoControl.ActivateCommitView();
			Dispatcher.UIThread.RunJobs();
			CommitUserControl commit = repoControl.Content.CommitUserControl;
			outCommit = commit;
			StageFileUserControl stage = commit.StageFileUserControl;
			Assert.True(UiClick.WaitFor(delegate
			{
				return stage.AllUnstagedFiles.Any(f => f.Path == filePath);
			}), "工作区状态未装配（未找到未暂存文件 " + filePath + "）");
			stage.UnstagedFilesFileListUserControl.SelectFile(filePath);
			Dispatcher.UIThread.RunJobs();
			MergeConflictUserControl conflict = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				conflict = UiClick.FindAll<MergeConflictUserControl>(window).FirstOrDefault();
				return conflict != null;
			}), "选中 Unmerged 文件后应出现 MergeConflictUserControl（CommitFileDiffControl 分发路径）");
			return conflict;
		}

		/// <summary>在模态泵内等待三方合并窗口出现并完成三编辑器装配，返回窗口实例。</summary>
		private static SideBySideMergeWindow WaitForMergeWindowLoaded()
		{
			SideBySideMergeWindow mergeWindow = null;
			int tries = 0;
			while (tries++ < 300)
			{
				Dispatcher.UIThread.RunJobs();
				mergeWindow = ForkPlus.UI.WpfCompat.WpfApp.Windows
					.OfType<SideBySideMergeWindow>().FirstOrDefault();
				if (mergeWindow != null
					&& mergeWindow.LocalMergeEditor.MergeConflictView != null
					&& mergeWindow.RemoteMergeEditor.MergeConflictView != null
					&& mergeWindow.MergedMergeEditor.MergeConflictView != null)
				{
					return mergeWindow;
				}
			}
			if (mergeWindow == null)
			{
				throw new InvalidOperationException("三方合并窗口未出现（模态泵 300 轮超时）");
			}
			throw new InvalidOperationException("三方编辑器视图未装配（三编辑器 MergeConflictView 应非空）");
		}

		/// <summary>模态泵内按本地化文案找 Footer 的 Resolve 提交按钮（SubmitButtonTitle）。</summary>
		private static Button FindResolveSubmitButton(SideBySideMergeWindow mergeWindow)
		{
			string title = E2eMainWindowHarness.Tr("Resolve");
			return UiClick.FindAll<Button>(mergeWindow)
				.FirstOrDefault(delegate (Button b) { return UiClick.ContentText(b) == title; });
		}

		// ==================================================================
		// 用例 1：冲突视图装配 + Resolve 按钮状态机
		// ==================================================================

		[Fact]
		public void MergeConflictView_LoadsAndResolveButtonStates()
		{
			string repo = TestRepoFactory.CreateConflict();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					MergeConflictUserControl conflict = OpenCommitViewAndWaitConflict(repo, "conflicted.txt", out var window, out _);
					try
					{
						// ===== 默认装配：未解决视图 + 双 CheckBox 勾选 + Merge 按钮启用 =====
						Assert.True(conflict.ConflictVersionsContainer.IsVisible, "未解决时版本选择容器应可见");
						Assert.False(conflict.ConflictResolvedContainer.IsVisible, "未解决时已解决容器应隐藏");
						Assert.Equal("conflicted.txt", conflict.FileNameTextBlock.FilePath);
						Assert.True(conflict.LocalCheckBox.IsChecked.GetValueOrDefault(), "IsMergeAllowed 时 ours 默认勾选");
						Assert.True(conflict.RemoteCheckBox.IsChecked.GetValueOrDefault(), "IsMergeAllowed 时 theirs 默认勾选");
						Assert.Equal(E2eMainWindowHarness.Tr("Merge"), UiClick.ContentText(conflict.ResolveButton));
						Assert.True(conflict.ResolveButton.IsEnabled, "双选 + 双侧未删除时 Merge 按钮应启用");
						// 双侧 Git 点视图（merge 进行中：ours=HEAD 侧、theirs=合并来源侧）
						Assert.True(conflict.LocalGitPointView.Value != null, "ours 侧 Git 点视图应装配");
						Assert.True(conflict.RemoteGitPointView.Value != null, "theirs 侧 Git 点视图应装配");
						ScreenshotHelper.Snap(window, "01-conflict-view", "10-mergeconflict");

						// ===== 状态机 1：仅勾 ours → Choose {ours 点名} =====
						UiClick.Toggle(conflict.RemoteCheckBox, false);
						string oursPoint = conflict.LocalGitPointView.Value.FriendlyName;
						Assert.Equal(E2eMainWindowHarness.TrFormat("Choose {0}", oursPoint),
							UiClick.ContentText(conflict.ResolveButton));
						Assert.True(conflict.ResolveButton.IsEnabled, "仅选一侧时按钮应启用（Choose 路径）");

						// ===== 状态机 2：仅勾 theirs → Choose {theirs 点名} =====
						UiClick.Toggle(conflict.LocalCheckBox, false);
						UiClick.Toggle(conflict.RemoteCheckBox, true);
						string theirsPoint = conflict.RemoteGitPointView.Value.FriendlyName;
						Assert.Equal(E2eMainWindowHarness.TrFormat("Choose {0}", theirsPoint),
							UiClick.ContentText(conflict.ResolveButton));
						Assert.True(conflict.ResolveButton.IsEnabled);

						// ===== 状态机 3：都不选 → 禁用 =====
						UiClick.Toggle(conflict.RemoteCheckBox, false);
						Assert.Equal(E2eMainWindowHarness.Tr("Merge"), UiClick.ContentText(conflict.ResolveButton));
						Assert.False(conflict.ResolveButton.IsEnabled, "两侧都不选时按钮应禁用");
						ScreenshotHelper.Snap(window, "02-resolve-disabled", "10-mergeconflict");
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

		// ==================================================================
		// 用例 2：Choose theirs（主入口）→ 真实解决冲突（git 双验证 + 暂存列表迁移）
		// 注：选 theirs 验证暂存迁移——checkout-index stage=3 的内容与 HEAD（ours）不同，
		// 才会出现 "M " 已暂存条目。选 ours 时解析结果与 HEAD 相同，文件会从两个列表都消失
		// （该语义由用例 3 的三方窗口全选 ours 覆盖），两条路径互补。
		// ==================================================================

		[Fact]
		public void MergeConflictView_ChooseTheirs_ResolvesConflictWithGitIndexVerification()
		{
			string repo = TestRepoFactory.CreateConflict();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					MergeConflictUserControl conflict = OpenCommitViewAndWaitConflict(repo, "conflicted.txt", out var window, out CommitUserControl commit);
					try
					{
						// ===== 仅勾 theirs → 点 Choose {theirs}：ResolveConflictGitCommand(Remote) =====
						UiClick.Toggle(conflict.LocalCheckBox, false);
					// 断言用 TrFormat("Choose {0}") 全量等值（与用例 1 同口径）——字典里没有裸
					// "Choose" 键，Tr("Choose") 回退英文原文，中文文案不含它（首跑实证误报）
					string theirsPointName = conflict.RemoteGitPointView.Value.FriendlyName;
					Assert.Equal(E2eMainWindowHarness.TrFormat("Choose {0}", theirsPointName),
						UiClick.ContentText(conflict.ResolveButton));
						UiClick.Click(conflict.ResolveButton);
						Dispatcher.UIThread.RunJobs();

						// ===== git 双验证：文件内容 = theirs 版本；索引已 add；无 unmerged =====
						string content = File.ReadAllText(Path.Combine(repo, "conflicted.txt"));
						Assert.Equal("their line\n", content);
						string status = TestRepoFactory.GitOutput(repo, "status --porcelain");
						Assert.False(status.Contains("UU"), "解决后不应再有 unmerged 状态条目，实际:\n" + status);
						Assert.True(status.Contains("M  conflicted.txt"), "checkout-index theirs + git add 后应为已暂存修改，实际:\n" + status);
						string staged = TestRepoFactory.GitOutput(repo, "diff --cached --name-only");
						Assert.Contains("conflicted.txt", staged);

						// ===== UI 验证：刷新后从未暂存（unmerged）列表移入已暂存列表 =====
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return !stage.AllUnstagedFiles.Any(f => f.Path == "conflicted.txt")
								&& stage.AllStagedFiles.Any(f => f.Path == "conflicted.txt");
						}), "解决后 conflicted.txt 应从未暂存列表移入已暂存列表（InvalidateAndRefresh）");
						ScreenshotHelper.Snap(window, "03-after-choose-theirs-staged", "10-mergeconflict");
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

		// ==================================================================
		// 用例 3：三方编辑器窗口 + 全选 ours + Resolve 提交（模态泵）
		// ==================================================================

		[Fact]
		public void SideBySideMergeWindow_ThreeWayEditors_SelectAllOurs_AndSubmit()
		{
			string repo = TestRepoFactory.CreateConflict();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					MergeConflictUserControl conflict = OpenCommitViewAndWaitConflict(repo, "conflicted.txt", out var window, out CommitUserControl commit);
					try
					{
						// ===== 模态泵：Post 处理器在 ShowDialog 的 DispatcherFrame 里驱动三方窗口 =====
					var handled = new bool[1];
					var handlerError = new string[1];
					Dispatcher.UIThread.Post(delegate
					{
						SideBySideMergeWindow mergeWindow = null;
						try
						{
							mergeWindow = WaitForMergeWindowLoaded();

							// ===== 三方编辑器装配：Local/Remote 侧内容 + Merged 侧冲突占位 =====
							Assert.Contains("our line", mergeWindow.LocalMergeEditor.MergeConflictView.StringValue);
							Assert.Contains("their line", mergeWindow.RemoteMergeEditor.MergeConflictView.StringValue);
							Assert.True(mergeWindow.MergedMergeEditor.Text.Contains("--- Merge Conflict ---"),
							"未解决时 Merged 视图应有冲突占位对齐行");

							// ===== 冲突状态徽标：0/1 + Resolve 禁用（IsSubmitAllowed→IsResolved）=====
							Assert.Equal("0/1", mergeWindow.MergeStatusTextBlock.Text);
							Button submit = FindResolveSubmitButton(mergeWindow);
							Assert.True(submit != null, "Footer 应有 Resolve 提交按钮（SubmitButtonTitle）");
							Assert.False(submit.IsEnabled, "冲突未解决时 Resolve 应禁用");
							ScreenshotHelper.Snap(mergeWindow, "04-sidebyside-window", "10-mergeconflict");

							// ===== 全选 ours：AllLocalCheckBox → SelectAll(Local) → 全解决 =====
							UiClick.Toggle(mergeWindow.AllLocalCheckBox, true);
							Assert.Equal("1/1", mergeWindow.MergeStatusTextBlock.Text);
							Assert.True(submit.IsEnabled, "全部冲突解决后 Resolve 应启用");
							Assert.True(mergeWindow.AllLocalCheckBox.IsChecked.GetValueOrDefault(),
								"RefreshCheckboxes 应保持全选 ours 勾选态");
							Assert.False(mergeWindow.AllRemoteCheckBox.IsChecked.GetValueOrDefault(),
								"选了 ours 行后全选 theirs 框应取消（RefreshCheckboxes 互斥）");
							ScreenshotHelper.Snap(mergeWindow, "05-select-all-ours", "10-mergeconflict");

							// ===== 提交：OnSubmit → ResolveMergeConflictGitCommand → Close =====
							UiClick.Click(submit);
							handled[0] = true;
						}
						catch (Exception ex)
						{
							handlerError[0] = ex.ToString();
							// 防死锁：handler 异常时必须关窗，否则模态 DispatcherFrame 永不退出
							//（用例 4 首轮实证：断言失败 → 窗口滞留 → dotnet test 无限挂起）
							try { mergeWindow?.Close(false); } catch { }
						}
					}, DispatcherPriority.Background);

					// 触发模态：双选 ResolveButton → ShowSideBySideMergeWindow → ShowDialog
					Assert.Equal(E2eMainWindowHarness.Tr("Merge"), UiClick.ContentText(conflict.ResolveButton));
					UiClick.Click(conflict.ResolveButton);

					Assert.True(handlerError[0] == null, "模态泵 handler 异常:\n" + handlerError[0]);
					Assert.True(handled[0], "三方窗口 handler 未执行（模态泵未推进？）");

					// ===== git 验证：文件 = ours 内容，无冲突标记，已 add =====
					Dispatcher.UIThread.RunJobs();
					string content = File.ReadAllText(Path.Combine(repo, "conflicted.txt"));
					Assert.Equal("our line\n", content);
					Assert.False(content.Contains("<<<<<<<") || content.Contains(">>>>>>>"), "解决后文件不应残留冲突标记");
					string status = TestRepoFactory.GitOutput(repo, "status --porcelain");
					Assert.False(status.Contains("UU"), "提交解决后不应有 unmerged，实际:\n" + status);
					// 全选 ours 的解析内容与 HEAD（ours 提交）相同 → git status 完全无此文件条目
					//（探针实证：checkout-index stage=2 + add 后 porcelain 为空——无差异不显示）
					Assert.False(status.Contains("conflicted.txt"), "解析结果与 HEAD 无差异时不应有任何状态条目，实际:\n" + status);

					// ===== UI 验证：无差异文件从两个列表都消失（未暂存移除 + 不进已暂存）=====
					StageFileUserControl stage = commit.StageFileUserControl;
					Assert.True(UiClick.WaitFor(delegate
					{
						return !stage.AllUnstagedFiles.Any(f => f.Path == "conflicted.txt")
							&& !stage.AllStagedFiles.Any(f => f.Path == "conflicted.txt");
					}), "全选 ours 解决后（内容=HEAD）conflicted.txt 应从未暂存列表消失且不进已暂存列表（InvalidateAndRefresh）");
					ScreenshotHelper.Snap(window, "06-after-merge-resolve-cleared", "10-mergeconflict");
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

		// ==================================================================
		// 用例 4：冲突块选择（多冲突块仓库，行级事件 = margin 点击生产等价入口）
		// ==================================================================

		[Fact]
		public void SideBySideMergeWindow_ConflictChunkSelection_PartialThenFullResolve()
		{
			string repo = TestRepoFactory.CreateConflictMulti();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					MergeConflictUserControl conflict = OpenCommitViewAndWaitConflict(repo, "multi.txt", out var window, out CommitUserControl commit);
					try
					{
						var handled = new bool[1];
						var handlerError = new string[1];
						Dispatcher.UIThread.Post(delegate
						{
							SideBySideMergeWindow mergeWindow = null;
							try
							{
								mergeWindow = WaitForMergeWindowLoaded();
							Assert.Equal("0/2", mergeWindow.MergeStatusTextBlock.Text);
							Button submit = FindResolveSubmitButton(mergeWindow);
							Assert.True(submit != null && !submit.IsEnabled, "两个冲突均未解决时 Resolve 应禁用");

							// ===== 第 1 块选 ours：行级选择事件（MergeLineNumberMargin 点击的等价入口）=====
							MergeConflictView.Chunk firstChunk = mergeWindow.LocalMergeEditor.MergeConflictView.Chunks
								.First(c => c.Node is MergeConflict.ConflictChunk);
							int firstLine = firstChunk.Lines[0].LineNumber;
							mergeWindow.LocalMergeEditor.OnMergeLineAdded(firstLine);
							Dispatcher.UIThread.RunJobs();
							Assert.Equal("1/2", mergeWindow.MergeStatusTextBlock.Text);
							Assert.False(submit.IsEnabled, "仅解决一半冲突时 Resolve 仍应禁用");
							ScreenshotHelper.Snap(mergeWindow, "07-partial-1-of-2", "10-mergeconflict");

							// ===== 第 2 块选 theirs：另一侧编辑器的行级选择事件 =====
							MergeConflictView.Chunk secondChunk = mergeWindow.RemoteMergeEditor.MergeConflictView.Chunks
								.Where(c => c.Node is MergeConflict.ConflictChunk).Skip(1).First();
							int secondLine = secondChunk.Lines[0].LineNumber;
							mergeWindow.RemoteMergeEditor.OnMergeLineAdded(secondLine);
							Dispatcher.UIThread.RunJobs();
							Assert.Equal("2/2", mergeWindow.MergeStatusTextBlock.Text);
							Assert.True(submit.IsEnabled, "两个冲突都解决后 Resolve 应启用");
								ScreenshotHelper.Snap(mergeWindow, "08-all-resolved-2-of-2", "10-mergeconflict");

								// ===== 提交：内容按块混合（block1 ours + block2 theirs）=====
								UiClick.Click(submit);
								handled[0] = true;
							}
							catch (Exception ex)
							{
								handlerError[0] = ex.ToString();
								// 防死锁：异常时必须关窗退出模态泵（见用例 3 注释）
								try { mergeWindow?.Close(false); } catch { }
							}
						}, DispatcherPriority.Background);

						UiClick.Click(conflict.ResolveButton);

						Assert.True(handlerError[0] == null, "模态泵 handler 异常:\n" + handlerError[0]);
						Assert.True(handled[0], "三方窗口 handler 未执行");

						// ===== git 验证：混合解决内容（行级选择落盘）=====
						Dispatcher.UIThread.RunJobs();
						string content = File.ReadAllText(Path.Combine(repo, "multi.txt"));
						string sep = "sep1\nsep2\nsep3\nsep4\nsep5\nsep6\nsep7\n";
						Assert.Equal("context start\nblock1 ours\n" + sep + "block2 theirs\ncontext end\n", content);
						string status = TestRepoFactory.GitOutput(repo, "status --porcelain");
						Assert.False(status.Contains("UU"), "提交后不应有 unmerged，实际:\n" + status);
						// 混合解析内容与 HEAD（全 ours）不同 → 应为已暂存修改
						Assert.True(status.Contains("M  multi.txt"), "混合解析与 HEAD 有差异应为已暂存修改，实际:\n" + status);

						// ===== UI 验证：混合解析后移入已暂存列表 =====
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllStagedFiles.Any(f => f.Path == "multi.txt")
								&& !stage.AllUnstagedFiles.Any(f => f.Path == "multi.txt");
						}), "混合解析后 multi.txt 应从未暂存列表移入已暂存列表（InvalidateAndRefresh）");
						ScreenshotHelper.Snap(window, "09-mixed-resolve-staged", "10-mergeconflict");
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

		// ==================================================================
		// 用例 5：滚动同步回归 + 冲突块导航（大文件冲突仓库）
		// ==================================================================

		[Fact]
		public void SideBySideMergeWindow_ScrollSync_AndChunkNavigation_Regression()
		{
			string repo = TestRepoFactory.CreateConflictLarge();
			MergerLayoutOrientation originalOrientation = ForkPlusSettings.Default.MergerLayoutOrientation;
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					MergeConflictUserControl conflict = OpenCommitViewAndWaitConflict(repo, "big.txt", out var window, out _);
					try
					{
						var handled = new bool[1];
					var handlerError = new string[1];
					Dispatcher.UIThread.Post(delegate
					{
						SideBySideMergeWindow mergeWindow = null;
						try
						{
							mergeWindow = WaitForMergeWindowLoaded();
							Assert.Equal("0/3", mergeWindow.MergeStatusTextBlock.Text);
								var local = mergeWindow.LocalMergeEditor;
								var remote = mergeWindow.RemoteMergeEditor;
								var merged = mergeWindow.MergedMergeEditor;
								ScreenshotHelper.Snap(mergeWindow, "09-sidebyside-large", "10-mergeconflict");

								// ===== 1) 垂直滚动同步：滚 Local 300px → Remote/Merged 跟随 =====
								local.ScrollToVerticalOffsetCompat(300.0);
								Dispatcher.UIThread.RunJobs();
								Assert.True(Math.Abs(local.TextArea.TextView.ScrollOffset.Y - 300.0) < 1.0,
									"Local 垂直滚动应到位（compat 入口），实际 " + local.TextArea.TextView.ScrollOffset.Y.ToString("F1"));
								Assert.True(Math.Abs(remote.TextArea.TextView.ScrollOffset.Y - 300.0) < 1.0,
									"Remote 应跟随垂直滚动，实际 " + remote.TextArea.TextView.ScrollOffset.Y.ToString("F1"));
								Assert.True(Math.Abs(merged.TextArea.TextView.ScrollOffset.Y - 300.0) < 1.0,
									"Merged 应跟随垂直滚动，实际 " + merged.TextArea.TextView.ScrollOffset.Y.ToString("F1"));

								// ===== 2) 水平滚动同步：滚 Merged 200px → Local/Remote 跟随 =====
								//（宽行分散放置：初始视口内即有宽行 → 水平 extent 存在）
								merged.ScrollToHorizontalOffsetCompat(200.0);
								Dispatcher.UIThread.RunJobs();
								Assert.True(Math.Abs(merged.TextArea.TextView.ScrollOffset.X - 200.0) < 1.0,
									"Merged 水平滚动应到位，实际 " + merged.TextArea.TextView.ScrollOffset.X.ToString("F1"));
								Assert.True(Math.Abs(local.TextArea.TextView.ScrollOffset.X - 200.0) < 1.0,
									"Local 应跟随水平滚动，实际 " + local.TextArea.TextView.ScrollOffset.X.ToString("F1"));
								Assert.True(Math.Abs(remote.TextArea.TextView.ScrollOffset.X - 200.0) < 1.0,
									"Remote 应跟随水平滚动，实际 " + remote.TextArea.TextView.ScrollOffset.X.ToString("F1"));
								ScreenshotHelper.Snap(mergeWindow, "10-scroll-synced", "10-mergeconflict");

								// ===== 3) 交替滚动防抖回归（100ms 守卫）：5 轮后三编辑器收敛一致 =====
								//（Sleep(150) 模拟真人换面板节奏，防回声守卫不吞同步——WPF 原版同款行为）
								double[] targets = { 500.0, 800.0, 650.0, 1000.0, 900.0 };
								for (int i = 0; i < targets.Length; i++)
								{
									Thread.Sleep(150);
									MergeCodeEditor editor = (i % 3) switch
									{
										0 => local,
										1 => remote,
										_ => merged,
									};
									editor.ScrollToVerticalOffsetCompat(targets[i]);
									Dispatcher.UIThread.RunJobs();
								}
								double finalY = local.TextArea.TextView.ScrollOffset.Y;
								Assert.True(Math.Abs(remote.TextArea.TextView.ScrollOffset.Y - finalY) < 1.0,
									"交替滚动后 Remote 应与 Local 收敛一致（防抖不应卡死同步），Local=" 
									+ finalY.ToString("F1") + " Remote=" + remote.TextArea.TextView.ScrollOffset.Y.ToString("F1"));
								Assert.True(Math.Abs(merged.TextArea.TextView.ScrollOffset.Y - finalY) < 1.0,
									"交替滚动后 Merged 应与 Local 收敛一致");

								// ===== 4) 冲突块导航：Next/Prev 基于视口中线行定位（WPF 原版同款 MiddleLine 算法）=====
							//（NextPrevMergeButtonsContainer 内 XAML 序：[0]=Previous [1]=Next）
							// 语义：Next = 中线行之后的下一个冲突块；Prev = 中线行之前的上一个冲突块。
							// 探针实证（WPF 原仓对照 + AvaloniaEdit 源码，非迁移回归）：ScrollToLine 走
							// ScrollTo(line, -1) → MinimumScrollFraction=0.3 守卫——目标与当前偏移差
							// < 0.3×视口高时抑制滚动。首块在文件顶部（行 11）时从顶点 Next 目标仅差
							// ~52px 被抑制（before=0 after=0），且中线行不动，重复点仍指首块（原版同款
							// 行为——真实用户此时首块已在视野内）。测试模拟"用户已聚焦首块"的真实态：
							// 把首块第二行滚到视口中线（compat 直设偏移绕过守卫），此后 Next → 第二块
							//（大位移过守卫）→ Prev → 回首块。中线落点容差：瞄准行 12，行高估算偏差
							// ±5% 内中线落行 11-13，三者 FindNextChunk 都会跳过首块（首块 Range.Start
							// 不大于这些行的 EndOffset），断言稳健。
							local.ScrollToVerticalOffsetCompat(0.0);
							remote.ScrollToVerticalOffsetCompat(0.0);
							merged.ScrollToVerticalOffsetCompat(0.0);
							Dispatcher.UIThread.RunJobs();
							Button next = UiClick.FindAll<Button>(mergeWindow.NextPrevMergeButtonsContainer)[1];
							Button prev = UiClick.FindAll<Button>(mergeWindow.NextPrevMergeButtonsContainer)[0];

							MergeConflictView.Chunk firstChunk = local.MergeConflictView.Chunks
								.First(c => c.Node is MergeConflict.ConflictChunk);
							int firstChunkLine = firstChunk.Lines[0].LineNumber;
							Avalonia.Controls.ScrollViewer localSv = UiClick.FindAll<Avalonia.Controls.ScrollViewer>(local)
								.First(s => s.Name == "PART_ScrollViewer");
							// 行高 = extent 高 / 行数（均匀单级行高；探针实证：估 17.55 与
							// ScrollToLine(63)→947.63px 的实测中心定位吻合）
							double lineHeight = localSv.Extent.Height / local.Document.LineCount;
							local.ScrollToVerticalOffsetCompat(
								firstChunkLine * lineHeight + lineHeight / 2.0 - localSv.Viewport.Height / 2.0);
							Dispatcher.UIThread.RunJobs();
							double beforeY = local.TextArea.TextView.ScrollOffset.Y;
							Assert.True(beforeY > 0.0,
								"聚焦首块应产生正偏移（中线对齐行 " + (firstChunkLine + 1) + "），实际 " + beforeY.ToString("F1"));

							UiClick.Click(next);
							Dispatcher.UIThread.RunJobs();
							double afterNextY = local.TextArea.TextView.ScrollOffset.Y;
							Assert.True(afterNextY > beforeY + 50.0,
								"Next 应滚动到下一个冲突块（行 62 附近），before=" + beforeY.ToString("F1")
								+ " after=" + afterNextY.ToString("F1"));
							// 守卫修复回归（2026-09-09，"合并结果栏从不滚到冲突块、停在文件开头"）：
							// 未解决冲突块在 Merged 视图没有内容行（ResultLines 为空，块内只有
							// "--- Merge Conflict ---" 对齐行），旧守卫 Lines.Length==0 直接 return，
							// Merged 永远停在文件开头；修复后经对齐行回退定位，Next 时 Merged 应同步滚到冲突块。
							double mergedAfterNextY = merged.TextArea.TextView.ScrollOffset.Y;
							Assert.True(mergedAfterNextY > 50.0,
								"Next 后 Merged 应滚到下一冲突块（对齐行回退定位，不再停在文件开头），实际 "
								+ mergedAfterNextY.ToString("F1"));
							UiClick.Click(prev);
							Dispatcher.UIThread.RunJobs();
							double afterPrevY = local.TextArea.TextView.ScrollOffset.Y;
							Assert.True(afterPrevY < afterNextY - 50.0,
								"Prev 应滚回上一个冲突块，afterNext=" + afterNextY.ToString("F1")
								+ " afterPrev=" + afterPrevY.ToString("F1"));
							// 同上：Merged 应回滚到上一冲突块（对齐行定位与 Local 内容行同块同步推进）
							double mergedAfterPrevY = merged.TextArea.TextView.ScrollOffset.Y;
							Assert.True(mergedAfterPrevY < mergedAfterNextY - 50.0,
								"Prev 后 Merged 应滚回上一冲突块，mergedAfterNext=" + mergedAfterNextY.ToString("F1")
								+ " mergedAfterPrev=" + mergedAfterPrevY.ToString("F1"));
								ScreenshotHelper.Snap(mergeWindow, "11-chunk-navigation", "10-mergeconflict");

								// ===== 5) 布局方向切换：Vertical ↔ Horizontal（Merged 编辑器 Grid 位置迁移）=====
							Assert.True(mergeWindow.LayoutOrientationToggleButton.IsChecked.GetValueOrDefault()
								== (originalOrientation == MergerLayoutOrientation.Vertical),
								"初始布局方向应与设置一致（" + originalOrientation + "）");
							mergeWindow.LayoutOrientationToggleButton.IsChecked =
								!(mergeWindow.LayoutOrientationToggleButton.IsChecked ?? false);
							Dispatcher.UIThread.RunJobs();
							MergerLayoutOrientation now = ForkPlusSettings.Default.MergerLayoutOrientation;
							Assert.True(originalOrientation != now, "切换后设置应更新");
								// Horizontal：Merged 独占整行（RowSpan 1/ColumnSpan 3）；Vertical：中列
								global::Avalonia.Controls.Grid grid = mergeWindow.MergedMergeEditor.Parent as global::Avalonia.Controls.Grid;
								Assert.True(grid != null, "Merged 编辑器应挂在布局 Grid 上");
								if (now == MergerLayoutOrientation.Horizontal)
								{
									Assert.Equal(3, global::Avalonia.Controls.Grid.GetColumnSpan(mergeWindow.MergedMergeEditor));
								}
								else
								{
									Assert.Equal(1, global::Avalonia.Controls.Grid.GetColumnSpan(mergeWindow.MergedMergeEditor));
								}
								ScreenshotHelper.Snap(mergeWindow, "12-orientation-toggled", "10-mergeconflict");

								// 不提交，直接取消关闭（模态返回 false 分支）
							mergeWindow.Close(false);
							handled[0] = true;
						}
						catch (Exception ex)
						{
							handlerError[0] = ex.ToString();
							// 防死锁：异常时必须关窗退出模态泵（见用例 3 注释）
							try { mergeWindow?.Close(false); } catch { }
						}
					}, DispatcherPriority.Background);

						UiClick.Click(conflict.ResolveButton);

						Assert.True(handlerError[0] == null, "模态泵 handler 异常:\n" + handlerError[0]);
						Assert.True(handled[0], "三方窗口 handler 未执行");

						// 取消路径：不解决冲突直接关闭 → git 状态不变（仍是 unmerged）
						string status = TestRepoFactory.GitOutput(repo, "status --porcelain");
						Assert.True(status.Contains("UU"), "取消关闭不应改动仓库状态");
					}
					finally
					{
						// 恢复布局方向设置并落盘（E2e07 教训：防持久化污染后续运行）
						ForkPlusSettings.Default.MergerLayoutOrientation = originalOrientation;
						ForkPlusSettings.Default.Save();
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
