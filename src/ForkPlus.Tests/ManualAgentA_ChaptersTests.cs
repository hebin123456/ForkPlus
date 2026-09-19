// 用户手册截图测试(子代理 A, 2026-09-19):为 ForkPlus 用户手册 ch-01 ~ ch-08 生成中文界面截图。
// 场景构造口径与 E2e01~E2e09/E2e28 参考测试一致(TestRepoFactory 建仓 + E2eMainWindowHarness
// OpenRepository 开主窗口 + UiClick 交互 + HeadlessAppBootstrap.Run),截图经
// ManualScreenshotHelper.Snap 落盘到 docs/manual/screenshots/<模块目录>/。
// 注意:本文件由主进程统一编译运行后入库,子代理只提交 docs/manual/ 下的章节与截图。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.Controls.Editor.Hex;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.BinaryDiff;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ManualAgentA_ChaptersTests
	{
		// ============================ ch-01 欢迎页与仓库管理 ============================

		[Fact]
		public void Ch01_WelcomeWindowAndRepositoryManager()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";

			// ----- 1) 欢迎窗口:初始表单 -----
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new WelcomeWindow();
				window.Show();
				Dispatcher.UIThread.RunJobs();
				ManualScreenshotHelper.Snap(window, "01-welcome-initial", "01-welcome");

				// 填表单(TextChanged 处理器会写入设置)
				var userName = UiClick.Find<PlaceholderTextBox>(window, "UserNameTextBox");
				var email = UiClick.Find<PlaceholderTextBox>(window, "EmailNameTextBox");
				var cloneDir = UiClick.Find<PlaceholderTextBox>(window, "DefaultCloneDirectoryTextBox");
				userName.Text = "Test User";
				email.Text = "test@example.com";
				cloneDir.Text = "/tmp/fp-clone-dir";
				Dispatcher.UIThread.RunJobs();
				ManualScreenshotHelper.Snap(window, "02-welcome-filled", "01-welcome");
				window.Close();
			});

			// ----- 2) 仓库管理列表(最近/仓库分组) -----
			string repoA = TestRepoFactory.CreateBasic();
			string repoB = TestRepoFactory.CreateBranches();
			try
			{
				var instance = global::ForkPlus.RepositoryManager.Instance;
				var prevRepos = instance.Repositories;
				var prevDirs = instance.SourceDirs;

				HeadlessAppBootstrap.Run(delegate
				{
					instance.RemoveAll();
					instance.SetSourceDirs(new string[0]);
					instance.AddRepositories(new[] { repoA, repoB });
					instance.AddOrUpdateLastOpened(repoA); // repoA 进 Recent

					var control = new RepositoryManagerUserControl();
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					ManualScreenshotHelper.Snap(window, "03-repomanager-with-repos", "01-welcome");

					// 点击选中仓库
					var tree = UiClick.Find<MultiselectionTreeView>(control, "RepositoriesTreeView");
					var containers = UiClick.FindAll<TreeViewControlItem>(tree)
						.Where(c => c.Node is RepositoryManagerRepositoryItem)
						.ToList();
					if (containers.Count >= 1)
					{
						var target = containers.First(c => PathNorm(((RepositoryManagerRepositoryItem)c.Node).Path) == PathNorm(repoA));
						UiClick.Press(target, window, new Avalonia.Point(60, 10));
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(window, "04-repomanager-selected", "01-welcome");
					}

					// 清空仓库 → 空态回退视图
					instance.RemoveAll();
					control.Refresh(restoreSelection: false);
					Dispatcher.UIThread.RunJobs();
					ManualScreenshotHelper.Snap(window, "05-repomanager-empty-fallback", "01-welcome");
					window.Close();
				});
				try
				{
					instance.RemoveAll();
					instance.AddRepositories(prevRepos.Select(r => r.Path).ToArray());
					instance.SetSourceDirs(prevDirs);
				}
				catch
				{
				}
			}
			finally
			{
				TestRepoFactory.Cleanup(repoA);
				TestRepoFactory.Cleanup(repoB);
			}

			// ----- 3) 多标签页(ClosableTabControl) -----
			HeadlessAppBootstrap.Run(delegate
			{
				var tabs = new ClosableTabControl();
				var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = tabs };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var tab1 = new ClosableTabItem { Content = new TextBlock { Text = "Tab One" } };
				var tab2 = new ClosableTabItem { Content = new TextBlock { Text = "Tab Two" } };
				var tab3 = new ClosableTabItem { Content = new TextBlock { Text = "Tab Three" } };
				tabs.AddTab(tab1);
				tabs.AddTab(tab2);
				tabs.AddTab(tab3);
				Dispatcher.UIThread.RunJobs();
				ManualScreenshotHelper.Snap(window, "06-tabs-three", "01-welcome");

				tabs.SelectTab(tab3);
				Dispatcher.UIThread.RunJobs();
				ManualScreenshotHelper.Snap(window, "07-tabs-third-selected", "01-welcome");
				window.Close();
			});
		}

		// ============================ ch-02 主界面总览 ============================

		[Fact]
		public void Ch02_MainWindowOverview()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateBranches();
			string repo2 = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// ===== 1) 主窗口全貌:工具栏 + 侧边栏 + 修订列表 + 修订详情 + 状态栏 =====
						var revList = repoControl.Content.RevisionListViewUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return revList.RevisionsDataSource.Count > 0;
						}), "修订列表未加载出数据(15s 超时)");
						ManualScreenshotHelper.Snap(window, "01-main-window-overview", "02-main-window");

						// ===== 2) 侧边栏:展开标签分组 =====
						var sidebar = repoControl.Sidebar;
						Assert.NotNull(sidebar);
						var treeView = sidebar.SidebarTreeView;
						var tags = Group(treeView, SidebarGroupItem.Group.Tags);
						bool loaded = UiClick.WaitFor(delegate
						{
							var bg = Group(treeView, SidebarGroupItem.Group.Branches);
							return bg != null && bg.Children.Count > 0;
						});
						Assert.True(loaded, "侧边栏分支分组未加载出数据(15s 超时)");
						if (tags != null)
						{
							tags.IsExpanded = true;
							Dispatcher.UIThread.RunJobs();
						}
						ManualScreenshotHelper.Snap(window, "02-main-window-sidebar", "02-main-window");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});

				// ===== 3) 多标签页:同一主窗口打开两个仓库 =====
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						bool openedSecond = window.TabManager.OpenRepository(repo2);
						Assert.True(openedSecond, "第二个仓库应打开为新的标签页");
						Dispatcher.UIThread.RunJobs();
						var secondRevList = repoControl.Content.RevisionListViewUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return secondRevList.RevisionsDataSource.Count > 0;
						}), "第二个标签页修订列表未加载(15s 超时)");
						ManualScreenshotHelper.Snap(window, "03-main-window-multi-tabs", "02-main-window");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
						E2eMainWindowHarness.CloseRepositoryTab(window, repo2);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
				TestRepoFactory.Cleanup(repo2);
			}
		}

		// ============================ ch-03 提交历史与修订列表 ============================

		[Fact]
		public void Ch03_RevisionListHistory()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				var orientationBefore = ForkPlusSettings.Default.RevisionListOrientation;
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var revList = repoControl.Content.RevisionListViewUserControl;
						bool loaded = UiClick.WaitFor(delegate
						{
							return revList.RevisionsDataSource.Count >= 5;
						});
						Assert.True(loaded, "修订列表未加载出数据(15s 超时)");
						int initialCount = revList.RevisionsDataSource.Count;

						// ===== 1) 图表视图(默认纵向布局) =====
						ManualScreenshotHelper.Snap(window, "01-revision-list-loaded", "03-history");

						// ===== 2) 行选中 → 下方显示该提交的变更文件列表 =====
						var listView = UiClick.Find<DragAndDropListView>(revList, "RevisionListView");
						listView.Select(0, NoUIAutomationListView.SelectOptions.ScrollIntoView);
						Dispatcher.UIThread.RunJobs();
						Assert.NotNull(revList.SelectedRevision);
						bool detailsLoaded = UiClick.WaitFor(delegate
						{
							return repoControl.Content.RevisionDetails.FullRevisionDetails != null;
						});
						Assert.True(detailsLoaded, "选中行后修订详情未加载(15s 超时)");
						ManualScreenshotHelper.Snap(window, "02-revision-row-selected", "03-history");

						// ===== 3) 上下文搜索(标记匹配 + 跳转,不缩小行集) =====
						var searchPanel = UiClick.Find<RevisionSearchPanelUserControl>(revList, "RevisionSearchPanelUserControl");
						searchPanel.ShowSearchBar();
						Dispatcher.UIThread.RunJobs();
						var searchBox = UiClick.Find<PlaceholderTextBox>(searchPanel, "SearchTextBox");
						searchBox.Text = "feature";
						bool searched = UiClick.WaitFor(delegate
						{
							return revList.RevisionsDataSource.ContextSearchCount > 0;
						});
						Assert.True(searched, "上下文搜索应标记到匹配(ContextSearchCount>0)");
						Assert.Equal(initialCount, revList.RevisionsDataSource.Count); // 行集不缩小
						ManualScreenshotHelper.Snap(window, "03-revision-search-matches", "03-history");

						// 清空搜索恢复
						searchBox.Text = "";
						bool restored = UiClick.WaitFor(delegate
						{
							return revList.RevisionsDataSource.ContextSearchCount == null
								|| revList.RevisionsDataSource.ContextSearchCount == 0;
						});
						Assert.True(restored, "清空搜索应清除匹配标记");
						searchPanel.HideSearchBar();
						Dispatcher.UIThread.RunJobs();

						// ===== 4) 方向切换(横向布局) =====
						new SwitchRevisionListOrientationCommand().Execute();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(window, "04-revision-orientation-switched", "03-history");
						// 还原设置
						new SwitchRevisionListOrientationCommand().Execute();
						ForkPlusSettings.Default.RevisionListOrientation = orientationBefore;
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
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ ch-04 修订详情 ============================

		[Fact]
		public void Ch04_RevisionDetails()
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
						var revList = repoControl.Content.RevisionListViewUserControl;
						bool loaded = UiClick.WaitFor(delegate
						{
							return revList.RevisionsDataSource.Count > 0;
						});
						Assert.True(loaded, "修订列表未加载出数据(15s 超时)");

						// 选中 c5 on feature/two(同秒提交下行序不保证,按主题扫行定位)
						int c5Row = -1;
						for (int row = 0; row < revList.RevisionsDataSource.Count; row++)
						{
							if (revList.RevisionsDataSource.GetDecoratedRevisionAtRow(row)?.Subject == "c5 on feature/two")
							{
								c5Row = row;
								break;
							}
						}
						Assert.True(c5Row >= 0, "应能定位到 c5 on feature/two 行");
						revList.Select(new int[1] { c5Row });
						var details = repoControl.Content.RevisionDetails;
						bool detailsLoaded = UiClick.WaitFor(delegate
						{
							return details.FullRevisionDetails != null
								&& details.FullRevisionDetails.RevisionDetails.Message.Trim() == "c5 on feature/two";
						});
						Assert.True(detailsLoaded, "c5 修订详情未加载");

						// ===== 1) 提交摘要 tab(作者/日期/消息/变更文件列表) =====
						ManualScreenshotHelper.Snap(window, "01-revision-details-summary", "04-revision-details");

						// ===== 2) 变更 tab(文件列表 + 选中文件 diff) =====
						details.ChangesRadioButton.IsChecked = true;
						Dispatcher.UIThread.RunJobs();
						var changes = details.ChangesUserControl;
						bool fileListLoaded = UiClick.WaitFor(delegate
						{
							return changes.FileListUserControl.Items.Length == 1;
						});
						Assert.True(fileListLoaded, "c5 的变更文件列表应加载出 two.txt");
						bool diffLoaded = UiClick.WaitFor(delegate
						{
							return changes.FileDiffControl.Content != null && changes.FileDiffControl.Content.Succeeded;
						});
						Assert.True(diffLoaded, "选中文件后 diff 内容应加载成功(15s 超时)");
						ManualScreenshotHelper.Snap(window, "02-revision-details-changes", "04-revision-details");

						// ===== 3) 文件树 tab =====
						details.FileTreeRadioButton.IsChecked = true;
						Dispatcher.UIThread.RunJobs();
						var fileTree = details.FileTreeUserControl;
						bool treeLoaded = UiClick.WaitFor(delegate
						{
							return fileTree.FilesTreeView.RootItem != null && fileTree.FilesTreeView.RootItem.Children.Count >= 2;
						});
						Assert.True(treeLoaded, "c5 时点文件树应含 main.txt/two.txt(15s 超时)");
						ManualScreenshotHelper.Snap(window, "03-revision-details-filetree", "04-revision-details");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});

				// ===== 4) Reflog 显示开关 =====
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var revList = repoControl.Content.RevisionListViewUserControl;
						bool loaded = UiClick.WaitFor(delegate
						{
							return revList.RevisionsDataSource.Count > 0;
						});
						Assert.True(loaded, "修订列表未加载出数据(15s 超时)");
						repoControl.ShowReflogInRevisionList = true;
						repoControl.InvalidateAndRefresh(SubDomain.Revisions);
						bool reflogOn = UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryData != null && repoControl.RepositoryData.Reflog;
						});
						Assert.True(reflogOn, "开启 reflog 后 RepositoryData.Reflog 应为 true(15s 超时)");
						ManualScreenshotHelper.Snap(window, "04-revision-details-reflog", "04-revision-details");
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

		// ============================ ch-05 变更与提交 ============================

		[Fact]
		public void Ch05_ChangesAndCommit()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateWorkingDir();
			DiffLayoutMode originalLayout = ForkPlusSettings.Default.CommitDiffLayoutMode;
			bool originalFullWorkdir = ForkPlusSettings.Default.ShowFullWorkingDirectory;
			try
			{
				ForkPlusSettings.Default.CommitDiffLayoutMode = DiffLayoutMode.Split;
				ForkPlusSettings.Default.ShowFullWorkingDirectory = false;
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;

						// ===== 1) 工作区状态:未暂存 3 项 + 已暂存 1 项 =====
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						}), "工作区状态未装配(15s 超时)");
						bool diffLoaded = UiClick.WaitFor(delegate
						{
							return commit.FileDiffControl.Content != null && commit.FileDiffControl.Content.Succeeded;
						});
						Assert.True(diffLoaded, "选中文件后 working dir diff 应加载成功(15s 超时)");
						ManualScreenshotHelper.Snap(window, "01-commit-working-dir", "05-commit");

						// ===== 2) 文件级暂存(Stage a.txt → 移入已暂存) =====
						stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						UiClick.Click(stage.StageButton);
						bool staged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 2 && stage.AllStagedFiles.Length == 2;
						});
						Assert.True(staged, "暂存 a.txt 后未暂存应剩 2 项、已暂存应为 2 项");
						ManualScreenshotHelper.Snap(window, "02-commit-stage-selected", "05-commit");

						// ===== 3) 文件级取消暂存(Unstage a.txt) =====
						stage.StagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						UiClick.Click(stage.UnstageButton);
						bool unstaged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						});
						Assert.True(unstaged, "取消暂存 a.txt 后应回到 3 未暂存 / 1 已暂存");
						ManualScreenshotHelper.Snap(window, "03-commit-unstage-selected", "05-commit");

						// ===== 4) 提交消息输入 + Amend 勾选 =====
						commit.FullCommitMessage = "write something\n";
						var desc = commit.CommitDescriptionTextBox;
						Dispatcher.UIThread.RunJobs();
						desc.Text = "Co-authored-by: Test <test@example.com>";
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(window, "04-commit-message-input", "05-commit");

						commit.AmendMode = true;
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(window, "05-commit-amend-checked", "05-commit");
						commit.AmendMode = false;
						Dispatcher.UIThread.RunJobs();

						// ===== 5) 文件列表模式切换(Tree 树形模式) =====
						stage.FileListsMode = FileListMode.Tree;
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(window, "06-commit-file-list-tree", "05-commit");
						stage.FileListsMode = FileListMode.List;
						Dispatcher.UIThread.RunJobs();

						// ===== 6) 行级暂存:选区浮窗(Stage / Discard...) =====
						stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						// 等 diff 加载链静默(照 E2e05):打开仓库后状态刷新会竞争性重载 working dir diff,
						// Content 引用连续 4 轮不变才算稳定——否则拿到旧 Content(前一个文件/重载前)
						// 的编辑器,选区文本对不上。
						object lastContent = null;
						int stableRounds = 0;
						Assert.True(UiClick.WaitFor(delegate
						{
							object current = commit.FileDiffControl.Content;
							if (current != null && object.ReferenceEquals(current, lastContent))
							{
								return ++stableRounds >= 4;
							}
							stableRounds = 0;
							lastContent = current;
							return false;
						}), "a.txt 的 working dir diff 加载未静默(后台状态刷新持续重载)");
						CommitCodeEditor editor = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							editor = UiClick.FindAll<CommitCodeEditor>(window).FirstOrDefault();
							return editor != null;
						}), "diff 编辑器(CommitCodeEditor)未出现在可视树");
						SelectLineAndShowFloatingButtons(window, editor, "line4-appended");
						FloatingButton stageBtn = WaitForFloatingButton(window, E2eMainWindowHarness.Tr("Stage"));
						FloatingButton discardBtn = WaitForFloatingButton(window, E2eMainWindowHarness.Tr("Discard..."));
						Assert.True(stageBtn != null, "未暂存 diff 选区浮窗应出现 Stage 按钮");
						Assert.True(discardBtn != null, "未暂存 diff 选区浮窗应出现 Discard... 按钮");
						ManualScreenshotHelper.Snap(window, "07-commit-line-level-floating", "05-commit");
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
				ForkPlusSettings.Default.ShowFullWorkingDirectory = originalFullWorkdir;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ ch-06 文本 Diff ============================

		[Fact]
		public void Ch06_TextDiff()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateLongLines();
			DiffLayoutMode originalMode = ForkPlusSettings.Default.CommitDiffLayoutMode;
			DiffLayoutMode originalPopupMode = ForkPlusSettings.Default.PopupDiffLayoutMode;
			try
			{
				ForkPlusSettings.Default.CommitDiffLayoutMode = DiffLayoutMode.Split;
				ForkPlusSettings.Default.PopupDiffLayoutMode = DiffLayoutMode.Split;
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
							return stage.AllUnstagedFiles.Length == 1;
						}), "工作区状态未装配(1 个未暂存文件 wide.txt)");
						stage.UnstagedFilesFileListUserControl.SelectFile("wide.txt");
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return commit.FileDiffControl.Content != null && commit.FileDiffControl.Content.Succeeded;
						}), "wide.txt 的 working dir diff 未加载");

						// ===== 1) Split 模式(单编辑器) =====
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<CommitCodeEditor>(window).Count == 1;
						}), "Split 模式应只有 1 个 CommitCodeEditor");
						ManualScreenshotHelper.Snap(window, "01-textdiff-split", "06-text-diff");

						// ===== 2) 生产入口切换 SideBySide(头部 DiffLayoutModeToggleButton) =====
						FileControlHeaderUserControl header = UiClick.FindAll<FileControlHeaderUserControl>(window).First();
						ToggleButton layoutToggle = UiClick.Find<ToggleButton>(header, "DiffLayoutModeToggleButton");
						Assert.NotNull(layoutToggle);
						layoutToggle.IsChecked = true;
						layoutToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<CommitCodeEditor>(window).Count == 2;
						}), "SideBySide 模式应有 2 个 CommitCodeEditor");
						ManualScreenshotHelper.Snap(window, "02-textdiff-side-by-side", "06-text-diff");

						// ===== 3) 双编辑器垂直滚动同步(右滚 → 左跟随) =====
						var editors = UiClick.FindAll<CommitCodeEditor>(window);
						CommitCodeEditor left = editors[0];
						CommitCodeEditor right = editors[1];
						right.ScrollToVerticalOffsetCompat(100.0);
						Dispatcher.UIThread.RunJobs();
						double leftY = left.TextArea.TextView.ScrollOffset.Y;
						double rightY = right.TextArea.TextView.ScrollOffset.Y;
						Assert.True(Math.Abs(leftY - rightY) < 1.0,
							"右侧垂直滚动后左侧应同步(left=" + leftY.ToString("F1") + " right=" + rightY.ToString("F1") + ")");
						ManualScreenshotHelper.Snap(window, "03-textdiff-scroll-synced", "06-text-diff");

						// ===== 4) 长行水平滚动(横向滚动条范围 + 滚动) =====
						right.ScrollToHorizontalOffsetCompat(200.0);
						Dispatcher.UIThread.RunJobs();
						double leftX = left.TextArea.TextView.ScrollOffset.X;
						double rightX = right.TextArea.TextView.ScrollOffset.X;
						Assert.True(rightX > 0.0, "右侧应真实水平滚动");
						Assert.True(Math.Abs(leftX - rightX) < 1.0, "右侧水平滚动后左侧应同步");
						ManualScreenshotHelper.Snap(window, "04-textdiff-horizontal-scrolled", "06-text-diff");

						// ===== 5) Diff 弹窗窗口(文件列表按 Space 打开) =====
						stage.UnstagedFilesFileListUserControl.RaiseEvent(new KeyEventArgs
						{
							RoutedEvent = InputElement.KeyDownEvent,
							Key = Key.Space
						});
						Dispatcher.UIThread.RunJobs();
						DiffPopupWindow popup = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							popup = WpfApp.Windows.OfType<DiffPopupWindow>().FirstOrDefault();
							return popup != null && popup.Title == "wide.txt"
								&& popup.FileDiffControl.Content != null && popup.FileDiffControl.Content.Succeeded;
						}), "Space 后应创建 DiffPopupWindow 并装配选中文件");
						ManualScreenshotHelper.Snap(popup, "05-diff-popup-window", "06-text-diff");
						CloseLeftoverPopups();
					}
					finally
					{
						CloseLeftoverPopups();
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.CommitDiffLayoutMode = originalMode;
				ForkPlusSettings.Default.PopupDiffLayoutMode = originalPopupMode;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ ch-07 二进制与图片 Diff ============================

		[Fact]
		public void Ch07_BinaryAndImageDiff()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			bool originalHighlight = ForkPlusSettings.Default.ImageDiffHighlightPixels;
			try
			{
				ForkPlusSettings.Default.ImageDiffHighlightPixels = false;

				// ----- 1) 图片对比:四模式 -----
				string imgRepo = TestRepoFactory.CreateImageDiff();
				try
				{
					HeadlessAppBootstrap.Run(delegate
					{
						BinaryDiffUserControl binaryDiff = OpenCommitViewAndWaitBinaryDiff(imgRepo, "img.png", out var window);
						try
						{
							// Side-by-Side(默认)
							Assert.True(UiClick.WaitFor(delegate
							{
								return binaryDiff.ViewModeButtonsContainer.IsVisible;
							}), "old/new 双图装配后 ViewModeButtonsContainer 应可见");
							ManualScreenshotHelper.Snap(window, "01-image-side-by-side", "07-binary-diff");

							// Swipe
							binaryDiff.SwipeRadioButton.IsChecked = true;
							Dispatcher.UIThread.RunJobs();
							Assert.True(UiClick.WaitFor(delegate
							{
								return binaryDiff.SwipeImageDiffView.IsVisible
									&& binaryDiff.SwipeImageDiffView.OverlayImage.ClipX.HasValue;
							}), "Swipe 视图应显示并装配 ClipX");
							ManualScreenshotHelper.Snap(window, "02-image-swipe", "07-binary-diff");

							// Onion Skin(不透明)
							binaryDiff.OnionSkinRadioButton.IsChecked = true;
							Dispatcher.UIThread.RunJobs();
							Assert.True(UiClick.WaitFor(delegate
							{
								return binaryDiff.OnionSkinImageDiffView.IsVisible;
							}), "OnionSkin 单选后应显示");
							ManualScreenshotHelper.Snap(window, "03-image-onionskin", "07-binary-diff");

							// 半透明对比
							binaryDiff.OnionSkinImageDiffView.Slider.Value = 0.5;
							Dispatcher.UIThread.RunJobs();
							ManualScreenshotHelper.Snap(window, "04-image-onionskin-half", "07-binary-diff");

							// Hex 视图(懒创建双 HexEditor)
							binaryDiff.HexRadioButton.IsChecked = true;
							Dispatcher.UIThread.RunJobs();
							Assert.True(UiClick.WaitFor(delegate
							{
								return binaryDiff.HexDiffViewContainer.IsVisible
									&& UiClick.FindAll<HexEditor>(window).Count >= 2;
							}), "Hex 视图应显示并装配双 HexEditor");
							ManualScreenshotHelper.Snap(window, "05-image-hex", "07-binary-diff");
						}
						finally
						{
							E2eMainWindowHarness.CloseRepositoryTab(window, imgRepo);
						}
					});
				}
				finally
				{
					TestRepoFactory.Cleanup(imgRepo);
				}

				// ----- 2) 非图片二进制:简略卡片视图 + Hex 视图 -----
				string binRepo = TestRepoFactory.CreateBinary();
				try
				{
					HeadlessAppBootstrap.Run(delegate
					{
						BinaryDiffUserControl cards = OpenCommitViewAndWaitBinaryDiff(binRepo, "data.bin", out var window);
						try
						{
							// 非图片二进制默认简略卡片视图(Side-by-Side + Hex 两个按钮,Swipe/Onion Skin 隐藏)
							Assert.True(!cards.SwipeRadioButton.IsVisible && !cards.OnionSkinRadioButton.IsVisible,
								"非图片二进制应隐藏 Swipe/Onion Skin");
							ManualScreenshotHelper.Snap(window, "06-binary-card-view", "07-binary-diff");

							cards.HexRadioButton.IsChecked = true;
							Dispatcher.UIThread.RunJobs();
							Assert.True(UiClick.WaitFor(delegate
							{
								return cards.HexDiffViewContainer.IsVisible
									&& UiClick.FindAll<HexEditor>(window).Count >= 2;
							}), "Hex 视图应有双 HexEditor");
							ManualScreenshotHelper.Snap(window, "07-binary-hex-view", "07-binary-diff");
						}
						finally
						{
							E2eMainWindowHarness.CloseRepositoryTab(window, binRepo);
						}
					});
				}
				finally
				{
					TestRepoFactory.Cleanup(binRepo);
				}

				// ----- 3) 大文件 diff(48KB→56KB,Hex 首屏截断出现"加载更多") -----
				string bigRepo = TestRepoFactory.CreateLargeBinary();
				try
				{
					HeadlessAppBootstrap.Run(delegate
					{
						BinaryDiffUserControl bigDiff = OpenCommitViewAndWaitBinaryDiff(bigRepo, "data.bin", out var window);
						try
						{
							bigDiff.HexRadioButton.IsChecked = true;
							Dispatcher.UIThread.RunJobs();
							Assert.True(UiClick.WaitFor(delegate
							{
								return bigDiff.HexDiffViewContainer.IsVisible
									&& UiClick.FindAll<HexEditor>(window).Count >= 2;
							}), "大文件 Hex 视图应装配双 HexEditor");
							ManualScreenshotHelper.Snap(window, "08-large-binary-hex", "07-binary-diff");
						}
						finally
						{
							E2eMainWindowHarness.CloseRepositoryTab(window, bigRepo);
						}
					});
				}
				finally
				{
					TestRepoFactory.Cleanup(bigRepo);
				}
			}
			finally
			{
				ForkPlusSettings.Default.ImageDiffHighlightPixels = originalHighlight;
				ForkPlusSettings.Default.Save();
			}
		}

		// ============================ ch-08 键盘快捷键 ============================

		[Fact]
		public void Ch08_KeyboardShortcuts()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new KeyboardShortcutsWindow();
				window.Show();
				Dispatcher.UIThread.RunJobs();
				ManualScreenshotHelper.Snap(window, "01-shortcuts-window", "08-shortcuts");
				window.Close();
			});
		}

		// ============================ 工具方法 ============================

		/// <summary>Commit 视图选中 unstaged 文件并等待二进制子视图(BinaryDiffUserControl)装配。</summary>
		private static BinaryDiffUserControl OpenCommitViewAndWaitBinaryDiff(string repo, string filePath, out MainWindow outWindow)
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
			}), "工作区状态未装配(未找到未暂存文件 " + filePath + ")");
			stage.UnstagedFilesFileListUserControl.SelectFile(filePath);
			Dispatcher.UIThread.RunJobs();
			BinaryDiffUserControl binaryDiff = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				binaryDiff = UiClick.FindAll<BinaryDiffUserControl>(window).FirstOrDefault();
				return binaryDiff != null;
			}), "选中 " + filePath + " 后应出现 BinaryDiffUserControl");
			return binaryDiff;
		}

		/// <summary>程序化选区并强制渲染一帧,令选区浮窗(Stage/Discard 悬浮按钮)出现。</summary>
		private static void SelectLineAndShowFloatingButtons(Window window, CommitCodeEditor editor, string lineText)
		{
			int selStart = editor.Text.IndexOf(lineText, StringComparison.Ordinal);
			Assert.True(selStart >= 0, "diff 文档中找不到 " + lineText);
			editor.Select(selStart, (lineText + "\n").Length);
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			// 强制真实渲染一帧:Render → DrawSelectionBorder → ShowChunkAdorner(选区顶部出浮窗)
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

		/// <summary>轮询等待选区浮窗按钮出现(每轮强制渲染一帧:浮窗经 Render → DrawSelectionBorder →
		/// ShowChunkAdorner 链路出现)。</summary>
		private static FloatingButton WaitForFloatingButton(Window window, string content)
		{
			FloatingButton button = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				HeadlessWindowExtensions.CaptureRenderedFrame(window);
				button = FindFloatingButton(window, content);
				return button != null;
			}), "选区浮窗按钮未出现:" + content);
			return button;
		}

		/// <summary>收尾保险:关闭可能残留的 diff 弹窗(弹窗泄漏进 lifetime.Windows 会殃及后续用例)。</summary>
		private static void CloseLeftoverPopups()
		{
			DiffPopupWindow[] popups = WpfApp.Windows.OfType<DiffPopupWindow>().ToArray();
			foreach (DiffPopupWindow popup in popups)
			{
				try
				{
					popup.Close();
				}
				catch
				{
					// 收尾尽力而为
				}
			}
			Dispatcher.UIThread.RunJobs();
		}

		private static SidebarGroupItem Group(MultiselectionTreeView treeView, SidebarGroupItem.Group groupType)
		{
			var root = treeView?.RootItem;
			if (root == null)
			{
				return null;
			}
			foreach (MultiselectionTreeViewItem child in root.Children)
			{
				if (child is SidebarGroupItem g && g.GroupType == groupType)
				{
					return g;
				}
			}
			return null;
		}

		private static string PathNorm(string p)
		{
			return p.Replace('\\', '/').TrimEnd('/');
		}
	}
}
