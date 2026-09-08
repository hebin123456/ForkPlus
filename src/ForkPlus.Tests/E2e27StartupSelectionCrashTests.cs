// E2E 模块27（2026-09-07）：启动选中索引越界崩溃回归（Linux 用户实测：某次重启后每次启动必崩）。
// 崩溃链路（用户完整堆栈逐帧实证）：
//   RepositoryManagerUserControl.Refresh → MultiselectionTreeViewItemCollection.Clear
//   → FlattenerNodeRemoved → Flattener_CollectionChanged → UpdateFocusedNode
//   → SelectedItems.Clear（AvaloniaList）→ InternalSelectionModel.OnSelectedItemsCollectionChanged
//   → CommitOperation → RaiseSelectionChanged → OnSelectionChanged 枚举 e.RemovedItems
//   （Avalonia 12 惰性索引视图）→ ItemsSourceView.GetAt(过期索引) → Flattener.get_Item 越界
//   → ArgumentOutOfRangeException 未处理 → 进程终止。
// 确定性触发形态（用户机器）：仓库管理器仅 1 个仓库（后台重扫描 AddRepositories 加入、
// 无 Opened 时间戳 → Recent 分组为空）→ Loaded → SelectFirstRepository 选中唯一仓库 =
// 扁平可见序列末项 → 重扫描完成回发 Refresh() → Children.Clear() 收缩源 → 末项索引必越界。
// 修复（MultiselectionTreeView 双层）：
//   ① OnSelectionChanged 不枚举事件参数（惰性索引视图），改按引用集合 SelectedItems 与
//      上次快照求差集同步节点 IsSelected（WPF 原语义等价）；
//   ② UpdateFocusedNode 的 SelectedIndex 回填加边界钳制（WPF 越界赋值静默忽略语义）。
// 截图 → docs/evidence/e2e/27-startup-selection-crash/。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e27StartupSelectionCrashTests
	{
		private static string PathNorm(string path)
		{
			return (path ?? "").Replace('\\', '/').TrimEnd('/');
		}

		// ============================ 控件级：源收缩下的选中链路 ============================

		private static FileListItem BuildTree(params string[] names)
		{
			var root = new FileListItem(new ChangedFile("", staged: false), "", null);
			foreach (string name in names)
			{
				root.Children.Add(new FileListItem(new ChangedFile(name, staged: false), name, null));
			}
			return root;
		}

		[Fact]
		public void TreeControl_SourceShrinkWithSelection_DoesNotCrash()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				// ===== 变体1：移除已选中的末项（Flattener Remove → UpdateFocusedNode 路径）=====
				// 修复前：SelectedItems.Clear() 提交的过期索引在 OnSelectionChanged 枚举
				// e.RemovedItems（惰性视图）时经 GetAt 越界崩溃（与用户堆栈同款）。
				{
					var tree = new MultiselectionTreeView();
					var window = new ForkPlus.UI.CustomWindow { Width = 400, Height = 300, Content = tree };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					FileListItem last = BuildTree("a", "b", "c", "d", "e");
					tree.RootItem = last;
					Dispatcher.UIThread.RunJobs();
					var selected = last.Children[4];
					tree.SelectedItems.Add(selected);
					Dispatcher.UIThread.RunJobs();
					Assert.True(selected.IsSelected, "选中同步：节点 IsSelected 应置 true");

					last.Children.Remove(selected); // 移除已选末项（索引 4 → 源收缩为 4 项）
					Dispatcher.UIThread.RunJobs(); // 修复前此处抛 ArgumentOutOfRangeException

					Assert.False(selected.IsSelected, "被移除节点应取消选中");
					Assert.Equal(last.Children[3], tree.SelectedItem); // 焦点上移语义保留（OldStartingIndex-1）
					window.Close();
				}

				// ===== 变体2：移除包含选中节点的尾部子树（侧边栏 Refilter/分组折叠路径）=====
				{
					var tree = new MultiselectionTreeView();
					var window = new ForkPlus.UI.CustomWindow { Width = 400, Height = 300, Content = tree };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					var root = BuildTree("a", "b");
					var folder = new FileListItem(new ChangedFile("dir/", staged: false), "dir", null);
					folder.Children.Add(new FileListItem(new ChangedFile("dir/f1", staged: false), "dir/f1", null));
					var deep = new FileListItem(new ChangedFile("dir/f2", staged: false), "dir/f2", null);
					folder.Children.Add(deep);
					root.Children.Add(folder);
					tree.RootItem = root;
					folder.IsExpanded = true;
					Dispatcher.UIThread.RunJobs();
					tree.SelectedItems.Add(deep); // 选中可见序列末项（dir/f2）
					Dispatcher.UIThread.RunJobs();

					root.Children.Remove(folder); // 整个尾部子树连选中节点一起移除
					Dispatcher.UIThread.RunJobs(); // 修复前越界崩溃

					Assert.False(deep.IsSelected, "子树移除后内部选中节点应取消选中");
					window.Close();
				}

				// ===== 变体3：RootItem 整体替换后清空并重选（FileList.SetItemSourceAsync 路径）=====
				{
					var tree = new MultiselectionTreeView();
					var window = new ForkPlus.UI.CustomWindow { Width = 400, Height = 300, Content = tree };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					var oldRoot = BuildTree("a", "b", "c", "d", "e");
					tree.RootItem = oldRoot;
					Dispatcher.UIThread.RunJobs();
					tree.SelectedItems.Add(oldRoot.Children[4]);
					Dispatcher.UIThread.RunJobs();

					var newRoot = BuildTree("x"); // 全量重建（ItemsSource 整体替换）
					tree.RootItem = newRoot;
					Dispatcher.UIThread.RunJobs();
					tree.SelectedItems.Clear(); // Select() 恢复选中第一步——修复前越界崩溃
					Dispatcher.UIThread.RunJobs();
					tree.SelectedItems.Add(newRoot.Children[0]); // 第二步重选——修复前同款越界
					Dispatcher.UIThread.RunJobs();

					Assert.True(newRoot.Children[0].IsSelected, "重建后应可重新建立选中");
					window.Close();
				}
			});
		}

		// ============================ 仓库管理器：用户确定性崩溃形态 ============================

		[Fact]
		public void RepositoryManager_SingleScannedRepo_RefreshWithTailSelection_DoesNotCrash()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateEmpty();
			try
			{
				var instance = RepositoryManager.Instance;
				// 快照全局状态，测试后还原
				var prevRepos = instance.Repositories;
				var prevDirs = instance.SourceDirs;

				HeadlessAppBootstrap.Run(delegate
				{
					// 用户形态：仅 1 个仓库、由重扫描加入（无 Opened 时间戳 → Recent 空）
					instance.RemoveAll();
					instance.SetSourceDirs(new string[0]);
					instance.AddRepositories(new[] { repo });

					var control = new RepositoryManagerUserControl();
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// Loaded → Recent 空 → SelectFirstRepository：唯一仓库被选中，
					// 且是扁平可见序列末项（确定性崩溃前置条件）
					Assert.NotNull(control.SelectedRepository);
					Assert.Equal(PathNorm(repo), PathNorm(control.SelectedRepository.Path));
					var tree = UiClick.Find<MultiselectionTreeView>(control, "RepositoriesTreeView");
					Assert.Equal(tree.Items.Count - 1, tree.SelectedIndex);

					// 重扫描回发的 Refresh（RescanUserRepositoriesCommand UIThread.Post 同款调用）
					// 修复前：Children.Clear() → 过期索引越界 → ArgumentOutOfRangeException
					control.Refresh();
					Dispatcher.UIThread.RunJobs();

					// restoreSelection 语义保留：按路径重选
					Assert.NotNull(control.SelectedRepository);
					Assert.Equal(PathNorm(repo), PathNorm(control.SelectedRepository.Path));
					ScreenshotHelper.Snap(window, "01-repomanager-single-repo-refresh-survived", "27-startup-selection-crash");
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
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void RepositoryManager_RecentSelection_RestoredAfterRefresh()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateEmpty();
			try
			{
				var instance = RepositoryManager.Instance;
				var prevRepos = instance.Repositories;
				var prevDirs = instance.SourceDirs;

				HeadlessAppBootstrap.Run(delegate
				{
					// 变体：仓库带 Opened 时间戳（进 Recent）——Loaded → SelectFirstRecent
					instance.RemoveAll();
					instance.SetSourceDirs(new string[0]);
					instance.AddRepositories(new[] { repo });
					instance.AddOrUpdateLastOpened(repo);

					var control = new RepositoryManagerUserControl();
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					Assert.NotNull(control.SelectedRepository);
					Assert.Equal(PathNorm(repo), PathNorm(control.SelectedRepository.Path));

					control.Refresh();
					Dispatcher.UIThread.RunJobs();

					// Refresh(restoreSelection: true) 按路径重选（SelectRepositoryItemWithPath）
					Assert.NotNull(control.SelectedRepository);
					Assert.Equal(PathNorm(repo), PathNorm(control.SelectedRepository.Path));
					ScreenshotHelper.Snap(window, "02-repomanager-recent-reselect", "27-startup-selection-crash");
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
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 完整启动路径 ============================

		[Fact]
		public void Startup_EmptySession_ManagerAutoSelectThenRefresh_DoesNotCrash()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateEmpty();
			try
			{
				var instance = RepositoryManager.Instance;
				var prevRepos = instance.Repositories;
				var prevDirs = instance.SourceDirs;

				// 用户启动形态：会话为空 → MainWindow 只建仓库管理器 tab；
				// 全局列表仅 1 个无 Opened 的仓库
				instance.RemoveAll();
				instance.SetSourceDirs(new string[0]);
				instance.AddRepositories(new[] { repo });

				HeadlessAppBootstrap.Run(delegate
				{
					// CreateWindow：真实 MainWindow + RestoreSession（空会话 → 仓库管理器 tab）
					var window = E2eMainWindowHarness.CreateWindow();
					var rmControl = window.GetVisualDescendants().OfType<RepositoryManagerUserControl>().FirstOrDefault();
					Assert.NotNull(rmControl);
					// Loaded 已自动选中唯一仓库（SelectFirstRepository → 扁平序列末项）
					Assert.NotNull(rmControl.SelectedRepository);
					Assert.Equal(PathNorm(repo), PathNorm(rmControl.SelectedRepository.Path));

					// 后台重扫描完成后的回发 Refresh——用户启动必崩点
					rmControl.Refresh();
					Dispatcher.UIThread.RunJobs();

					Assert.NotNull(rmControl.SelectedRepository);
					Assert.Equal(PathNorm(repo), PathNorm(rmControl.SelectedRepository.Path));
					ScreenshotHelper.Snap(window, "03-startup-manager-refresh-survived", "27-startup-selection-crash");
					E2eMainWindowHarness.DetachWindow(window);
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
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
