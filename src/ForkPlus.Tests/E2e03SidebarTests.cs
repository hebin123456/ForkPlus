// E2E 模块3（2026-09-05）：侧边栏。
// 覆盖：分组结构（分支嵌套文件夹/tags/stashes）、分组与文件夹展开收起、
// 过滤框（防抖→Refilter→IsHidden）、分支右键菜单与分组右键菜单（真实构建管线）。
// 截图 → docs/evidence/e2e/03-sidebar/。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e03SidebarTests
	{
		[Fact]
		public void Sidebar_GroupsExpandCollapseAndFilter()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				var module = new GitModule(repo, System.IO.Path.Combine(repo, ".git"), null, null);
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new RepositoryUserControl();
					control.OpenRepository(module);
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// ===== 1) 真实管线加载（InvalidateAndRefresh 同时触发 Sidebar 延迟创建） =====
					control.InvalidateAndRefresh(SubDomain.All);
					var sidebar = control.Sidebar;
					Assert.NotNull(sidebar);
					var treeView = sidebar.SidebarTreeView;
					bool loaded = UiClick.WaitFor(delegate
					{
						var bg = BranchGroup(treeView);
						return bg != null && bg.Children.Count > 0;
					});
					Assert.True(loaded, "侧边栏分支分组未加载出数据（15s 超时）");

					// ===== 2) 分组结构断言（真实 git 读取：main + feature/{one,two} 嵌套文件夹 + 2 tag） =====
					var branches = BranchGroup(treeView);
					var mainItem = branches.Children.OfType<LocalBranchSidebarItem>().FirstOrDefault(b => b.Title == "main");
					Assert.True(mainItem != null, "Branches 分组应有 main 分支");
					Assert.True(mainItem.LocalBranch.IsActive, "main 应是当前活跃分支");
					var featureFolder = branches.Children.OfType<FilterableFolderSidebarItem>().FirstOrDefault(f => f.Title == "feature");
					Assert.True(featureFolder != null, "feature/one、feature/two 应聚合为 feature 文件夹");
					Assert.True(featureFolder.Children.Count == 2, "feature 文件夹应含 one/two 两个分支，实际 " + featureFolder.Children.Count);
					var tags = Group(treeView, SidebarGroupItem.Group.Tags);
					Assert.True(tags.Children.Count == 2, "Tags 分组应有 v1.0/v2.0 两个 tag，实际 " + tags.Children.Count);
					ScreenshotHelper.Snap(window, "01-sidebar-loaded", "03-sidebar");

					// ===== 3) 文件夹收起/展开（生产首开行为：FilterString null→"" 初始化触发
					//      ExpandAllChildren，feature 文件夹默认展开——与产品语义一致） =====
					var oneItem = featureFolder.Children.OfType<LocalBranchSidebarItem>().FirstOrDefault(b => b.Title == "one");
					Assert.True(oneItem != null, "feature 文件夹应含 one 分支");
					// 首开展开是异步三级链：FilterTextBox 初始化 → 0.1s System.Timers.Timer（线程池）
					// → Dispatcher.Post 回 UI 线程 → FilterString null→"" → ExpandAllChildren → IsVisible 传播。
					// 上面的 WaitFor 只等了数据层（Branches.Children 非空），慢 runner 上视觉层
					// 可能尚未传播完，立即断言 IsVisible 会 flaky（CI 实证：a972c63 纯文档提交挂于此）。
					bool oneVisible = UiClick.WaitFor(delegate
					{
						return oneItem.IsVisible;
					});
					Assert.True(oneVisible, "首开全展开策略下 one 分支应可见（15s 超时）");
					Assert.True(oneItem.LocalBranch.Name.Contains("feature/one"), "分支名应含 feature/one，实际 " + oneItem.LocalBranch.Name);
					ScreenshotHelper.Snap(window, "02-sidebar-folder-expanded", "03-sidebar");

					featureFolder.IsExpanded = false; // 属性 setter 即生产完整管线（OnCollapsing→持久化）
					Dispatcher.UIThread.RunJobs();
					Assert.False(oneItem.IsVisible, "收起后 one 分支应不可见");
					ScreenshotHelper.Snap(window, "03-sidebar-folder-collapsed", "03-sidebar");
					featureFolder.IsExpanded = true;
					Dispatcher.UIThread.RunJobs();
					Assert.True(oneItem.IsVisible, "重新展开后 one 分支应恢复可见");

					// ===== 4) 分组收起/展开（Branches 分组收起 → 整组子项不可见） =====
					branches.IsExpanded = false;
					Dispatcher.UIThread.RunJobs();
					Assert.False(mainItem.IsVisible, "Branches 分组收起后 main 应不可见");
					Assert.False(featureFolder.IsVisible, "Branches 分组收起后 feature 文件夹应不可见");
					ScreenshotHelper.Snap(window, "04-sidebar-group-collapsed", "03-sidebar");
					branches.IsExpanded = true;
					Dispatcher.UIThread.RunJobs();
					Assert.True(mainItem.IsVisible, "Branches 分组重新展开后 main 应可见");

					// ===== 5) Tags 分组展开（默认不展开） =====
					tags.IsExpanded = true;
					Dispatcher.UIThread.RunJobs();
					var tagItem = tags.Children.OfType<TagSidebarItem>().FirstOrDefault(t => t.Title == "v1.0");
					Assert.True(tagItem != null, "Tags 分组应含 v1.0");
					Assert.True(tagItem.IsVisible, "Tags 展开后 v1.0 应可见");
					Assert.StartsWith("refs/tags/", tagItem.Reference.FullReference);
					ScreenshotHelper.Snap(window, "05-sidebar-tags-expanded", "03-sidebar");

					// ===== 6) 过滤框（0.1s 防抖 → UpdateFilter → FilterString → Refilter） =====
					var filterBox = sidebar.FilterTextBox;
					filterBox.Text = "feat"; // "main" 不含 feat → 隐藏；feature/one、feature/two 的 Reference.Name 含 feat → 保留
					bool filtered = UiClick.WaitFor(delegate
					{
						return treeView.FilterString == "feat";
					});
					Assert.True(filtered, "过滤防抖后 FilterString 应更新为 feat");
					Assert.True(mainItem.IsHidden, "main 不匹配 feat 应被过滤隐藏");
					Assert.False(featureFolder.IsHidden, "feature 文件夹应保留（自身+子项均匹配）");
					Assert.True(featureFolder.IsExpanded, "过滤时全树应自动展开");
					ScreenshotHelper.Snap(window, "06-sidebar-filtered", "03-sidebar");

					// ===== 7) 清空过滤恢复 =====
					filterBox.Text = "";
					bool restored = UiClick.WaitFor(delegate
					{
						return string.IsNullOrEmpty(treeView.FilterString) && !mainItem.IsHidden;
					});
					Assert.True(restored, "清空过滤后应恢复全部条目");
					Assert.False(mainItem.IsHidden, "清空后 main 应恢复可见");
					ScreenshotHelper.Snap(window, "07-sidebar-filter-cleared", "03-sidebar");
					window.Close();
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void Sidebar_ContextMenusAndStashes()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repo = TestRepoFactory.CreateBranches();
			string stashRepo = TestRepoFactory.CreateStash();
			try
			{
				var module = new GitModule(repo, System.IO.Path.Combine(repo, ".git"), null, null);
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new RepositoryUserControl();
					control.OpenRepository(module);
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.InvalidateAndRefresh(SubDomain.All);
					var sidebar = control.Sidebar;
					var treeView = sidebar.SidebarTreeView;
					bool loaded = UiClick.WaitFor(delegate
					{
						return BranchGroup(treeView) != null && BranchGroup(treeView).Children.Count > 0;
					});
					Assert.True(loaded, "侧边栏未加载（15s 超时）");

					// ===== 1) 分支右键（真实管线：右键 PointerPressed → 菜单构建 → Open） =====
					var branches = BranchGroup(treeView);
					var mainItem = branches.Children.OfType<LocalBranchSidebarItem>().First(b => b.Title == "main");
					var mainContainer = UiClick.FindAll<TreeViewControlItem>(treeView)
						.FirstOrDefault(c => ReferenceEquals(c.Node, mainItem));
					Assert.True(mainContainer != null, "main 分支行容器应已实现化（分支默认展开且在视口）");
					RightClick(mainContainer, window, new Avalonia.Point(60, 8));
					var menu = treeView.ContextMenu;
					Assert.True(menu != null, "侧边栏树应配置 ContextMenu");
					Assert.True(menu.Items.Count >= 5, "本地分支右键菜单项应 >= 5，实际 " + menu.Items.Count);
					string menuText = string.Join("\n", menu.Items.OfType<MenuItem>().Select(MenuHeaderText));
					Assert.True(menuText.Contains("Checkout") || menuText.Contains("检出"), "菜单应含 Checkout/检出 项: " + menuText);
					Assert.True(menuText.Contains("Delete") || menuText.Contains("删除"), "菜单应含 Delete/删除 项: " + menuText);
					Assert.True(ReferenceEquals(mainItem, treeView.LastClickedItem), "右键应设置 LastClickedItem");
					ScreenshotHelper.Snap(window, "08-sidebar-branch-context-menu", "03-sidebar");
					menu.Close();

					// ===== 2) 分组头右键（Branches 分组 → 新建分支等分组菜单） =====
					var branchesContainer = UiClick.FindAll<TreeViewControlItem>(treeView)
						.FirstOrDefault(c => ReferenceEquals(c.Node, branches));
					Assert.True(branchesContainer != null, "Branches 分组头容器应已实现化");
					RightClick(branchesContainer, window, new Avalonia.Point(60, 8));
					Assert.True(menu.Items.Count >= 2, "分组右键菜单项应 >= 2，实际 " + menu.Items.Count);
					string groupMenuText = string.Join("\n", menu.Items.OfType<MenuItem>().Select(MenuHeaderText));
					Assert.True(groupMenuText.Contains("Branch") || groupMenuText.Contains("分支"), "分组菜单应含分支相关项: " + groupMenuText);
					menu.Close();
					window.Close();
				});

				// ===== 3) stash 分组（独立仓库：2 条 stash 条目默认展开可见） =====
				var stashModule = new GitModule(stashRepo, System.IO.Path.Combine(stashRepo, ".git"), null, null);
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new RepositoryUserControl();
					control.OpenRepository(stashModule);
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.InvalidateAndRefresh(SubDomain.All);
					var sidebar = control.Sidebar;
					var treeView = sidebar.SidebarTreeView;
					var stashes = Group(treeView, SidebarGroupItem.Group.Stashes);
					bool stashLoaded = UiClick.WaitFor(delegate
					{
						return stashes != null && stashes.Children.Count >= 2;
					});
					Assert.True(stashLoaded, "Stashes 分组应加载出 2 条 stash（15s 超时）");
					Assert.True(stashes.IsExpanded, "Stashes 分组默认展开");
					string titles = string.Join(",", stashes.Children.Select(c => c.Title));
					Assert.True(titles.Contains("stash-one") && titles.Contains("stash-two"), "应含两条 stash 消息，实际: " + titles);
					ScreenshotHelper.Snap(window, "09-sidebar-stashes", "03-sidebar");
					window.Close();
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
				TestRepoFactory.Cleanup(stashRepo);
			}
		}

		// ============================ 工具 ============================

		/// <summary>右键按下（走 Tunnel|Bubble 双路由 + handledEventsToo 的生产处理器管线）。</summary>
		private static void RightClick(InputElement container, Window window, Avalonia.Point position)
		{
			var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
			var properties = new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed);
			container.RaiseEvent(new PointerPressedEventArgs(container, pointer, window, position, (ulong)Environment.TickCount64, properties, KeyModifiers.None));
			Dispatcher.UIThread.RunJobs();
		}

		private static string MenuHeaderText(MenuItem item)
		{
			return item.Header as string ?? string.Empty;
		}

		private static SidebarGroupItem BranchGroup(MultiselectionTreeView treeView)
		{
			return Group(treeView, SidebarGroupItem.Group.Branches);
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
	}
}
