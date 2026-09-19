// 用户手册截图测试(子代理 B, 2026-09-19):为 ForkPlus 用户手册 ch-09 ~ ch-16 生成中文界面截图。
// 场景构造口径与 E2e10~E2e16/E2e19 参考测试一致(TestRepoFactory 建仓 + E2eMainWindowHarness
// OpenRepository 开主窗口 + UiClick 交互 + HeadlessAppBootstrap.Run),截图经
// ManualScreenshotHelper.Snap 落盘到 docs/manual/screenshots/<模块目录>/。
// 注意:本文件由主进程统一编译运行后入库,子代理只提交 docs/manual/ 下的章节与截图。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ManualAgentB_ChaptersTests
	{
		// ============================ 共享助手 ============================

		/// <summary>等引用/工作区状态装配完成(后台 git 读取经 Dispatcher 回 UI)。</summary>
		private static RepositoryReferences WaitForRefs(RepositoryUserControl control, int minBranches = 3)
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

		/// <summary>命令预览文本(ForkPlusDialogWindow.AddCommandPreview 生成的 Consolas TextBlock)。</summary>
		private static string CommandPreviewOf(ForkPlusDialogWindow dialog)
		{
			return dialog.GetVisualDescendants().OfType<TextBlock>()
				.FirstOrDefault(t => t.Text != null && t.Text.StartsWith("git ", StringComparison.Ordinal))?.Text ?? "";
		}

		private static LocalBranch BranchNamed(RepositoryReferences references, string name)
		{
			return references.LocalBranches.First(b => b.Name == name);
		}

		private static Tag TagNamed(RepositoryReferences references, string name)
		{
			return references.Tags.First(t => t.Name == name);
		}

		/// <summary>按 sha 取 Revision(生产命令 GetRevisionsGitCommand 真实管线)。</summary>
		private static Revision RevisionFor(GitModule gitModule, string sha)
		{
			Assert.True(Sha.TryParse(sha, out Sha parsed), "sha 应可解析: " + sha);
			GitCommandResult<Revision[]> result = new GetRevisionsGitCommand().Execute(gitModule, new Sha[] { parsed });
			Assert.True(result.Succeeded, "GetRevisionsGitCommand 应成功: " + (result.Error?.FriendlyDescription ?? ""));
			Assert.Single(result.Result);
			return result.Result[0];
		}

		/// <summary>SaveAsPatchWindow 装配用 Revision(生产命令 GetRevisionsInRangeGitCommand 单修订查询)。</summary>
		private static Revision RevisionInRangeOf(GitModule gitModule, string sha)
		{
			Assert.True(Sha.TryParse(sha, out Sha parsed), "sha 应可解析: " + sha);
			GitCommandResult<GetRevisionsInRangeGitCommand.Result> result =
				new GetRevisionsInRangeGitCommand().Execute(gitModule, parsed, null);
			Assert.True(result.Succeeded, "GetRevisionsInRange 应成功: " + (result.Error?.FriendlyDescription ?? ""));
			Assert.Single(result.Result.Revisions);
			return result.Result.Revisions[0];
		}

		private static Sha ParseSha(string sha)
		{
			Assert.True(Sha.TryParse(sha, out Sha parsed), "sha 应可解析: " + sha);
			return parsed;
		}

		/// <summary>RI 辅助进程环境保障(ForkPlus.RI 继承本进程环境;DOTNET_ROOT 缺失时 apphost
		/// 找不到 .NET 运行时——从当前运行时目录向上三级推导)。</summary>
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

		/// <summary>打开仓库并切到 Commit 视图,选中冲突文件,等 MergeConflictUserControl 装配。</summary>
		private static MergeConflictUserControl OpenCommitViewAndWaitConflict(
			string repo, string filePath, out MainWindow outWindow)
		{
			RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out MainWindow window);
			outWindow = window;
			repoControl.ActivateCommitView();
			Dispatcher.UIThread.RunJobs();
			CommitUserControl commit = repoControl.Content.CommitUserControl;
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
				return conflict != null && conflict.FileNameTextBlock.FilePath == filePath;
			}), "选中 Unmerged 文件后应出现并完成装配 MergeConflictUserControl");
			return conflict;
		}

		/// <summary>在模态泵内等待三方合并窗口出现并完成三编辑器装配。</summary>
		private static SideBySideMergeWindow WaitForMergeWindowLoaded()
		{
			SideBySideMergeWindow mergeWindow = null;
			int tries = 0;
			while (tries++ < 300)
			{
				Dispatcher.UIThread.RunJobs();
				mergeWindow = WpfApp.Windows.OfType<SideBySideMergeWindow>().FirstOrDefault();
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

		// ============================ ch-09 分支操作 ============================

		[Fact]
		public void Ch09_Branches()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForRefs(repoControl, 3);
						LocalBranch featureOne = BranchNamed(references, "feature/one");
						LocalBranch featureTwo = BranchNamed(references, "feature/two");

						// 1) 主窗口(分支列表/侧栏/提交图)
						ManualScreenshotHelper.Snap(window, "01-branch-list", "09-branches");

						// 2) 创建分支:合法新名 + 检出后切换命令预览
						var create = new CreateBranchWindow(repoControl, references, references.ActiveBranch);
						create.Show();
						Dispatcher.UIThread.RunJobs();
						create.BranchNameTextBox.Text = "feature/three";
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(create, "02-create-branch", "09-branches");
						create.Close();

						// 3) 检出分支
						var checkout = new CheckoutBranchWindow(repoControl, featureOne, null);
						checkout.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(checkout, "03-checkout-branch", "09-branches");
						checkout.Close();

						// 4) 重命名分支
						var rename = new RenameLocalBranchWindow(repoControl.GitModule, references, featureTwo, null);
						rename.Show();
						Dispatcher.UIThread.RunJobs();
						rename.BranchNameTextBox.Text = "feature/two-renamed";
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(rename, "04-rename-branch", "09-branches");
						rename.Close();

						// 5) 删除本地分支
						var remove = new RemoveLocalBranchWindow(repoControl, references,
							new LocalBranch[] { featureOne }, repoControl.RepositoryData.Remotes);
						remove.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(remove, "05-remove-local-branch", "09-branches");
						remove.Close();

						// 6) checkout as worktree(路径自动派生)
						var worktree = new CheckoutBranchAsWorktreeWindow(repoControl, featureOne);
						worktree.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(worktree, "06-checkout-as-worktree", "09-branches");
						worktree.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		[Fact]
		public void Ch09b_BranchTrackingAndMultiPush()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateRemoteBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForRefs(repoControl, 3);
						RemoteBranch remoteOnly = WaitForRemoteBranch(repoControl, "origin/remote-only");
						Remote origin = repoControl.RepositoryData.Remotes.Items.First(r => r.Name == "origin");
						LocalBranch one = BranchNamed(references, "feature/one");
						LocalBranch two = BranchNamed(references, "feature/two");

						// 1) 跟踪远程分支
						var track = new TrackRemoteBranchWindow(repoControl, references.LocalBranches, remoteOnly);
						track.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(track, "07-track-remote-branch", "09-branches");
						track.Close();

						// 2) 多分支推送(新上游列表)
						var pushMulti = new PushMultipleBranchesWindow(repoControl, new LocalBranch[] { one, two }, origin);
						pushMulti.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(pushMulti, "08-push-multiple-branches", "09-branches");
						pushMulti.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ ch-10 标签操作 ============================

		[Fact]
		public void Ch10_Tags()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateTags();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForTags(repoControl, 2);

						// 1) 创建标签:合法新名 + 消息命令预览
						var create = new CreateTagWindow(repoControl.GitModule, references,
							repoControl.RepositoryData.Remotes.Items, references.ActiveBranch);
						create.Show();
						Dispatcher.UIThread.RunJobs();
						create.TagNameTextBox.Text = "v3.0.0";
						create.TagMessageTextBox.Text = "release notes";
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(create, "01-create-tag", "10-tags");
						create.Close();

						// 2) 标签详情:附注标签
						var ann = new TagDetailsWindow(repoControl.GitModule, TagNamed(references, "ann-1.0"));
						ann.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(ann, "02-tag-details-annotated", "10-tags");
						ann.Close();

						// 3) 标签详情:轻量标签
						var light = new TagDetailsWindow(repoControl.GitModule, TagNamed(references, "light-2.0"));
						light.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(light, "03-tag-details-lightweight", "10-tags");
						light.Close();

						// 4) 删除多标签(列表模式)
						var remove = new RemoveTagWindow(repoControl,
							new Tag[] { TagNamed(references, "ann-1.0"), TagNamed(references, "light-2.0") }, references);
						remove.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(remove, "04-remove-tags-multi", "10-tags");
						remove.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		[Fact]
		public void Ch10b_TagPushAndRemoteDelete()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateRemoteTags();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForTags(repoControl, 5);

						// 1) 创建并推送(推送开关预览)
						var createPush = new CreateTagWindow(repoControl.GitModule, references,
							repoControl.RepositoryData.Remotes.Items, references.ActiveBranch);
						createPush.Show();
						Dispatcher.UIThread.RunJobs();
						createPush.TagNameTextBox.Text = "rel-new";
						createPush.TagMessageTextBox.Text = "pushed release";
						Dispatcher.UIThread.RunJobs();
						UiClick.Toggle(createPush.PushCheckBox, true);
						ManualScreenshotHelper.Snap(createPush, "05-create-tag-push", "10-tags");
						createPush.Close();

						// 2) 删除单标签(从远程删除)
						var remove = new RemoveTagWindow(repoControl, new Tag[] { TagNamed(references, "rel-1") }, references);
						remove.Show();
						Dispatcher.UIThread.RunJobs();
						UiClick.Toggle(remove.DeleteFromRemotesCheckBox, true);
						ManualScreenshotHelper.Snap(remove, "06-remove-tag-remote", "10-tags");
						remove.Close();

						// 3) 单标签推送
						var push = new PushTagWindow(repoControl, TagNamed(references, "rel-3"), null);
						push.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(push, "07-push-tag", "10-tags");
						push.Close();

						// 4) 多标签推送
						var pushMulti = new PushMultipleTagsWindow(repoControl,
							new Tag[] { TagNamed(references, "rel-4"), TagNamed(references, "rel-5") }, null);
						pushMulti.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(pushMulti, "08-push-tags-multi", "10-tags");
						pushMulti.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ ch-11 历史改写 ============================

		[Fact]
		public void Ch11_HistoryRewrite()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						RepositoryReferences references = WaitForRefs(repoControl, 2);
						LocalBranch feature = BranchNamed(references, "feature");
						LocalBranch main = references.ActiveBranch;

						// 1) 合并:干净合并默认预览
						var merge = new MergeBranchWindow(repoControl, feature, main);
						merge.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(merge, "01-merge-branch", "11-history-rewrite");

						// 2) 合并:No Fast-Forward 预览
						MergeBranchWindow.MergeOptionComboBoxItem noFF = merge.MergeTypeComboBox.ItemsSource
							.OfType<MergeBranchWindow.MergeOptionComboBoxItem>()
							.First(i => i.Title == "No Fast-Forward");
						merge.MergeTypeComboBox.SelectedItem = noFF;
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(merge, "02-merge-no-ff", "11-history-rewrite");
						merge.Close();

						// 3) 拣选:单提交预览
						string f2Sha = TestRepoFactory.GitOutput(repo, "rev-parse feature~1").Trim();
						string f1Sha = TestRepoFactory.GitOutput(repo, "rev-parse feature~2").Trim();
						Revision f2 = RevisionFor(repoControl.GitModule, f2Sha);
						var cherry = new CherryPickWindow(repoControl, new Revision[] { f2 }, new Sha[] { ParseSha(f1Sha) });
						cherry.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(cherry, "03-cherry-pick", "11-history-rewrite");
						cherry.Close();

						// 4) 还原:预览
						Revision baseTwo = RevisionFor(repoControl.GitModule,
							TestRepoFactory.GitOutput(repo, "rev-parse main").Trim());
						Revision baseOne = RevisionFor(repoControl.GitModule,
							TestRepoFactory.GitOutput(repo, "rev-parse main~1").Trim());
						var revert = new RevertRevisionWindow(repoControl, baseTwo, new Sha[] { ParseSha(baseOne.Sha.ToString()) });
						revert.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(revert, "04-revert", "11-history-rewrite");
						revert.Close();

						// 5) 重置:hard 预览
						var reset = new ResetBranchWindow(repoControl, main, baseOne);
						reset.Show();
						Dispatcher.UIThread.RunJobs();
						reset.ResetTypeCombobox.SelectedIndex = 2;
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(reset, "05-reset-hard", "11-history-rewrite");
						reset.Close();

						// 6) Reflog 窗口
						var reflog = new ReflogWindow(repoControl);
						reflog.Show();
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return reflog.ReflogListView.ItemsSource != null
								&& reflog.ReflogListView.ItemsSource.OfType<ReflogViewItem>().Count() > 0;
						}), "reflog 条目应装配（15s 超时）");
						ManualScreenshotHelper.Snap(reflog, "06-reflog-window", "11-history-rewrite");
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

		[Fact]
		public void Ch11b_RebaseAndInteractiveRebase()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			try
			{
				EnsureDotnetRootForRiHelper();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					InteractiveRebaseWindow ir = null;
					try
					{
						RepositoryReferences references = WaitForRefs(repoControl, 2);
						LocalBranch feature = references.ActiveBranch;
						LocalBranch main = BranchNamed(references, "main");

						// 1) 变基:干净变基预览
						var rebase = new RebaseBranchWindow(repoControl, feature, main);
						rebase.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(rebase, "07-rebase-preview", "11-history-rewrite");
						rebase.Close();

						// 2) 交互式变基:todo 列表装配(真实 git rebase -i + ForkPlus.RI IPC)
						ir = new InteractiveRebaseWindow(repoControl, repoControl.GitModule, feature, main, null);
						ir.Show();
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return ir.RevisionListView.ItemsSource != null
								&& ir.RevisionListView.ItemsSource.OfType<RevisionEntry>().Count() == 3;
						}), "todo 列表应装配 3 个提交（RI→IPC→GetRebaseTodoListCommand，15s 超时）");
						ManualScreenshotHelper.Snap(ir, "08-interactive-rebase", "11-history-rewrite");
					}
					finally
					{
						try
						{
							if (ir != null)
							{
								if (ir.IsVisible)
								{
									typeof(InteractiveRebaseWindow).GetMethod("StopRebaseInteractiveProcess",
										System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
										?.Invoke(ir, new object[] { "cancel" });
									UiClick.WaitFor(delegate { return !ir.IsVisible; }, 5000);
								}
								ir.Dispose();
							}
						}
						catch
						{
							// 兜底尽力而为,不掩盖断言
						}
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ ch-12 Stash 贮藏 ============================

		[Fact]
		public void Ch12_Stash()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateStashWork();
			bool savedStageNewFiles = ForkPlusSettings.Default.SaveStash_StageNewFiles;
			try
			{
				ForkPlusSettings.Default.SaveStash_StageNewFiles = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 1) 保存 stash:消息 + 含未跟踪文件预览
						var save = new SaveStashWindow(repoControl.GitModule);
						save.Show();
						Dispatcher.UIThread.RunJobs();
						save.StashMessageTextBox.Text = "wip stash";
						Dispatcher.UIThread.RunJobs();
						UiClick.Toggle(save.StageNewFilesCheckBox, true);
						ManualScreenshotHelper.Snap(save, "01-save-stash", "12-stash");
						save.Close();

						// 2) 部分保存:文件列表勾选
						ChangedFile[] all = ChangedFilesOf(repoControl.GitModule);
						ChangedFile[] toStash = all.Where(f => f.Path == "a.txt").ToArray();
						var partial = new CreatePartialStashWindow(repoControl.GitModule, toStash, all);
						partial.Show();
						Dispatcher.UIThread.RunJobs();
						partial.StashMessageTextBox.Text = "partial wip";
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(partial, "02-partial-stash", "12-stash");
						partial.Close();
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

		[Fact]
		public void Ch12b_StashApplyRemoveRename()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
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
						StashRevision top = stashes.Items.First(s => s.ReflogName == "stash@{0}");
						StashRevision second = stashes.Items.First(s => s.ReflogName == "stash@{1}");

						// 1) 应用 stash(apply 保留条目)
						var apply = new ApplyStashWindow(repoControl, top);
						apply.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(apply, "03-apply-stash", "12-stash");
						apply.Close();

						// 2) 应用 stash(pop 删除条目)
						var pop = new ApplyStashWindow(repoControl, top);
						pop.Show();
						Dispatcher.UIThread.RunJobs();
						UiClick.Toggle(pop.DeleteStashAfterApplyCheckBox, true);
						ManualScreenshotHelper.Snap(pop, "04-apply-pop", "12-stash");
						pop.Close();

						// 3) 删除 stash(单模式)
						var remove = new RemoveStashWindow(repoControl, new[] { second });
						remove.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(remove, "05-remove-stash", "12-stash");
						remove.Close();

						// 4) 删除 stash(多模式)
						var removeMulti = new RemoveStashWindow(repoControl, stashes.Items);
						removeMulti.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(removeMulti, "06-remove-stashes-multi", "12-stash");
						removeMulti.Close();

						// 5) 重命名 stash
						var rename = new RenameStashWindow(repoControl, top);
						rename.Show();
						Dispatcher.UIThread.RunJobs();
						rename.StashNameTextBox.Text = "renamed stash two";
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(rename, "07-rename-stash", "12-stash");
						rename.Close();
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

		/// <summary>生产命令取工作区变更文件(CreatePartialStashWindow 构造入参的同一数据源)。</summary>
		private static ChangedFile[] ChangedFilesOf(GitModule gitModule)
		{
			GitCommandResult<ChangedFilesCollection> result = new GetChangedFilesGitCommand().Execute(gitModule);
			Assert.True(result.Succeeded, "GetChangedFilesGitCommand 应成功: " + (result.Error?.FriendlyDescription ?? ""));
			return result.Result.ChangedFiles;
		}

		// ============================ ch-13 远程交互 ============================

		[Fact]
		public void Ch13_Remotes()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string work = TestRepoFactory.CreateRemoteBehind();
			bool savedFetchAllRemotes = ForkPlusSettings.Default.Fetch_FetchAllRemotes;
			bool savedFetchAllTags = ForkPlusSettings.Default.FetchAllTags;
			bool savedRebase = ForkPlusSettings.Default.Pull_Rebase;
			bool savedStash = ForkPlusSettings.Default.Pull_StashAndReapply;
			try
			{
				ForkPlusSettings.Default.Fetch_FetchAllRemotes = false;
				ForkPlusSettings.Default.FetchAllTags = false;
				ForkPlusSettings.Default.Pull_Rebase = false;
				ForkPlusSettings.Default.Pull_StashAndReapply = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						// 1) Fetch 默认预览
						var fetch = new FetchWindow(repoControl, repoControl.GitModule, null);
						fetch.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(fetch, "01-fetch", "13-remotes");

						// 2) Fetch all remotes 预览
						UiClick.Toggle(fetch.FetchAllRemotesCheckBox, true);
						ManualScreenshotHelper.Snap(fetch, "02-fetch-all", "13-remotes");
						fetch.Close();

						// 3) Pull 默认预览
						var pull = new PullWindow(repoControl, null);
						pull.Show();
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate { return FooterOf(pull).SubmitButton.IsEnabled; }),
							"Pull 弹窗应完成引用加载并启用提交（15s 超时）");
						ManualScreenshotHelper.Snap(pull, "03-pull", "13-remotes");

						// 4) Pull --rebase 预览
						UiClick.Toggle(pull.RebaseCheckBox, true);
						ManualScreenshotHelper.Snap(pull, "04-pull-rebase", "13-remotes");
						pull.Close();
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
				ForkPlusSettings.Default.Pull_Rebase = savedRebase;
				ForkPlusSettings.Default.Pull_StashAndReapply = savedStash;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(work);
			}
		}

		[Fact]
		public void Ch13b_PushEditRemoteRefspec()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string work = TestRepoFactory.CreateBareRemote();
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
						// 1) Push 默认预览(已有上游)
						var push = new PushWindow(repoControl);
						push.Show();
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate { return FooterOf(push).SubmitButton.IsEnabled; }),
							"Push 弹窗应完成装配并启用提交（15s 超时）");
						ManualScreenshotHelper.Snap(push, "05-push", "13-remotes");
						push.Close();

						// 2) EditRemote Add 模式
						var add = new EditRemoteWindow(repoControl, repoControl.GitModule);
						add.Show();
						Dispatcher.UIThread.RunJobs();
						add.RemoteNameTextBox.Text = "backup";
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(add, "06-edit-remote-add", "13-remotes");
						add.Close();

						// 3) EditRemote Edit 模式(预填 + set-url 预览)
						Remote origin = repoControl.RepositoryData.Remotes.Items.First(r => r.Name == "origin");
						var edit = new EditRemoteWindow(repoControl, repoControl.GitModule, origin);
						edit.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(edit, "07-edit-remote-edit", "13-remotes");
						edit.Close();

						// 4) 自定义 refspec
						var refspec = new AddCustomRefspecWindow("origin", "feature");
						refspec.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(refspec, "08-custom-refspec", "13-remotes");
						refspec.Close();
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

		[Fact]
		public void Ch13c_PushNewBranch()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string work = TestRepoFactory.CreateBareRemote();
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
						var push = new PushWindow(repoControl);
						push.Show();
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate { return FooterOf(push).SubmitButton.IsEnabled; }),
							"Push 弹窗应完成装配并启用提交（15s 超时）");
						ManualScreenshotHelper.Snap(push, "09-push-new-branch", "13-remotes");
						push.Close();
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

		// ============================ ch-14 合并冲突解决 ============================

		[Fact]
		public void Ch14_Conflicts()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateConflict();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					MergeConflictUserControl conflict = OpenCommitViewAndWaitConflict(repo, "conflicted.txt", out var window);
					try
					{
						// 1) 冲突视图:双版本选择 + Merge 按钮
						ManualScreenshotHelper.Snap(window, "01-conflict-view", "14-conflicts");

						// 2) 仅勾 theirs → Choose {theirs} 状态
						UiClick.Toggle(conflict.LocalCheckBox, false);
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(window, "02-choose-theirs", "14-conflicts");

						// 3) 三方并排编辑器(模态泵)
						var handled = new bool[1];
						var handlerError = new string[1];
						Dispatcher.UIThread.Post(delegate
						{
							SideBySideMergeWindow mergeWindow = null;
							try
							{
								mergeWindow = WaitForMergeWindowLoaded();
								ManualScreenshotHelper.Snap(mergeWindow, "03-side-by-side-merge", "14-conflicts");
								mergeWindow.Close(false);
								handled[0] = true;
							}
							catch (Exception ex)
							{
								handlerError[0] = ex.ToString();
								try { mergeWindow?.Close(false); } catch { }
							}
						}, DispatcherPriority.Background);

						// 恢复双选(Merge 按钮)再点开三方窗口
						UiClick.Toggle(conflict.LocalCheckBox, true);
						Dispatcher.UIThread.RunJobs();
						UiClick.Click(conflict.ResolveButton);

						Assert.True(handlerError[0] == null, "模态泵 handler 异常:\n" + handlerError[0]);
						Assert.True(handled[0], "三方窗口 handler 未执行（模态泵未推进？）");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ ch-15 子模块与 Worktree ============================

		[Fact]
		public void Ch15_SubmoduleWorktree()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string work = TestRepoFactory.CreateBasic();
			string subsrc = TestRepoFactory.CreateSubmoduleSource();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						// 1) 添加子模块:URL + 路径齐备
						var add = new AddSubmoduleWindow(repoControl.GitModule,
							new SubmodulesToUpdate(new Tuple<Submodule, bool>[0]));
						add.Show();
						Dispatcher.UIThread.RunJobs();
						add.RepositoryUrlTextBox.Text = subsrc;
						add.PathTextBox.Text = "sub";
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(add, "01-add-submodule", "15-submodule-worktree");
						add.Close();
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

		[Fact]
		public void Ch15b_SubmoduleDiffAndDelete()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateSubmodule();
			string root = Directory.GetParent(repo).FullName;
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 1) 子模块 diff 视图(指针前进 + 工作区脏)
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 1 && stage.AllUnstagedFiles[0].Path == "sub";
						}), "工作区状态应装配唯一的未暂存变更 sub（15s 超时）");
						stage.UnstagedFilesFileListUserControl.SelectFile("sub");
						Dispatcher.UIThread.RunJobs();
						SubmoduleDiffUserControl subDiff = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							subDiff = UiClick.FindAll<SubmoduleDiffUserControl>(window).FirstOrDefault();
							return subDiff != null;
						}), "选中 sub 后应出现子模块 diff 视图（15s 超时）");
						ManualScreenshotHelper.Snap(window, "02-submodule-diff", "15-submodule-worktree");

						// 2) 删除子模块(deinit + rm 双命令预览)
						Submodule sub = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							sub = repoControl.RepositoryData?.Submodules?.Items.FirstOrDefault(s => s.Path == "sub");
							return sub != null;
						}), "子模块列表应装配 sub（15s 超时）");
						var del = new DeleteSubmoduleWindow(repoControl.GitModule, sub);
						del.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(del, "03-delete-submodule", "15-submodule-worktree");
						del.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(root); }
		}

		[Fact]
		public void Ch15c_WorktreeCreateCheckoutDelete()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateWithWorktree();
			string root = Directory.GetParent(repo).FullName;
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 等装配:main + feature/two 分支 + wt-one worktree
						LocalBranch main = null;
						LocalBranch featureTwo = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							var branches = repoControl.RepositoryData?.References.LocalBranches;
							main = branches?.FirstOrDefault(b => b.Name == "main");
							featureTwo = branches?.FirstOrDefault(b => b.Name == "feature/two");
							return main != null && featureTwo != null
								&& repoControl.RepositoryData.Worktrees.Items.Any(w => w.FriendlyName == "wt-one");
						}), "分支与 worktree 数据应装配（15s 超时）");

						// 1) 创建 worktree(新分支 + 自动派生路径)
						var create = new CreateWorktreeWindow(repoControl, main);
						create.Show();
						Dispatcher.UIThread.RunJobs();
						create.BranchNameTextBox.Text = "wtbranch";
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(create, "04-create-worktree", "15-submodule-worktree");
						create.Close();

						// 2) 检出分支为 worktree
						var checkout = new CheckoutBranchAsWorktreeWindow(repoControl, featureTwo);
						checkout.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(checkout, "05-checkout-as-worktree", "15-submodule-worktree");
						checkout.Close();

						// 3) 删除 worktree
						Worktree? wt = repoControl.RepositoryData.Worktrees.Items
							.FirstOrDefault(w => w.FriendlyName == "wt-one");
						Assert.True(wt.HasValue, "worktree 列表应含 wt-one");
						var del = new DeleteWorktreeWindow(repoControl, wt.Value);
						del.Show();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(del, "06-delete-worktree", "15-submodule-worktree");
						del.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(root); }
		}

		// ============================ ch-16 补丁与快照 ============================

		[Fact]
		public void Ch16_PatchSnapshot()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
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
						string f3 = TestRepoFactory.GitOutput(repo, "rev-parse HEAD").Trim();
						string f2 = TestRepoFactory.GitOutput(repo, "rev-parse HEAD~1").Trim();

						// 1) 保存补丁:单修订
						var single = new SaveAsPatchWindow(repoControl, gitModule,
							RevisionInRangeOf(gitModule, f3), null);
						single.Show();
						Assert.True(UiClick.WaitFor(delegate
						{
							return single.RevisionsItemsControl.ItemsSource is Revision[] revs && revs.Length == 1;
						}), "revisions 列表应装配 1 条（15s 超时）");
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(single, "01-save-as-patch", "16-patch-snapshot");
						single.Close();

						// 2) 保存补丁:范围模式
						var range = new SaveAsPatchWindow(repoControl, gitModule,
							RevisionInRangeOf(gitModule, f3), ParseSha(f2));
						range.Show();
						Assert.True(UiClick.WaitFor(delegate
						{
							return range.RevisionsItemsControl.ItemsSource is Revision[] revs && revs.Length == 2;
						}), "revisions 列表应装配 2 条（15s 超时）");
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(range, "02-save-as-patch-range", "16-patch-snapshot");
						range.Close();
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

		[Fact]
		public void Ch16b_ApplyPatchAndSnapshot()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: false);
			string outDir = Path.Combine(Path.GetTempPath(), "fpman apply dir " + Guid.NewGuid().ToString("N").Substring(0, 6));
			bool savedStageNewFiles = ForkPlusSettings.Default.SaveStash_StageNewFiles;
			try
			{
				// 生成补丁:新增文件 new.txt(intent-to-add + git diff)
				string patchPath = Path.Combine(outDir, "new-file.patch");
				Directory.CreateDirectory(outDir);
				File.WriteAllText(Path.Combine(repo, "new.txt"), "patched line\n");
				TestRepoFactory.GitOutput(repo, "add -N new.txt");
				File.WriteAllText(patchPath, TestRepoFactory.GitOutput(repo, "diff"));
				TestRepoFactory.GitOutput(repo, "reset -q");
				File.Delete(Path.Combine(repo, "new.txt"));

				// 剪贴板补丁:format-patch From 头(f1)
				byte[] patchData = System.Text.Encoding.UTF8.GetBytes(
					TestRepoFactory.GitOutput(repo, "format-patch -1 feature~2 --stdout"));

				ForkPlusSettings.Default.SaveStash_StageNewFiles = false;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 1) 应用补丁:文件路径模式
						var fileApply = new ApplyPatchWindow(repoControl, patchPath);
						fileApply.Show();
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return FooterOf(fileApply).SubmitButton.IsEnabled;
						}), "存在文件时提交应启用（15s 超时）");
						ManualScreenshotHelper.Snap(fileApply, "03-apply-patch-file", "16-patch-snapshot");
						fileApply.Close();

						// 2) 应用补丁:剪贴板模式(git am 预览)
						var clipApply = new ApplyPatchWindow(repoControl, patchData);
						clipApply.Show();
						Dispatcher.UIThread.RunJobs();
						UiClick.Toggle(clipApply.CreateCommitsCheckBox, true);
						ManualScreenshotHelper.Snap(clipApply, "04-apply-patch-clipboard", "16-patch-snapshot");
						clipApply.Close();
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
				if (Directory.Exists(outDir))
				{
					Directory.Delete(outDir, recursive: true);
				}
			}
		}

		[Fact]
		public void Ch16c_SaveSnapshot()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateStashWork();
			bool savedStageNewFiles = ForkPlusSettings.Default.SaveStash_StageNewFiles;
			try
			{
				ForkPlusSettings.Default.SaveStash_StageNewFiles = true;
				ForkPlusSettings.Default.Save();
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var snap = new SaveSnapshotWindow(repoControl);
						snap.Show();
						Dispatcher.UIThread.RunJobs();
						snap.StashMessageTextBox.Text = "snap msg";
						Dispatcher.UIThread.RunJobs();
						UiClick.Toggle(snap.StageNewFilesCheckBox, true);
						ManualScreenshotHelper.Snap(snap, "05-save-snapshot", "16-patch-snapshot");
						snap.Close();
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
	}
}
