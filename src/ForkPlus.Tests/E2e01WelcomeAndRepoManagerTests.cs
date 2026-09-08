// E2E 模块1（2026-09-05）：启动与仓库管理。
// 覆盖：WelcomeWindow 表单、RepositoryManagerUserControl（分组/选中/空态回退）、ClosableTabControl 增删选。
// 截图 → docs/evidence/e2e/01-welcome/。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e01WelcomeAndRepoManagerTests
	{
		// ============================ WelcomeWindow ============================

		[Fact]
		public void WelcomeWindow_FormAndRender()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new ForkPlus.UI.Dialogs.WelcomeWindow();
				window.Show();
				Dispatcher.UIThread.RunJobs();
				ScreenshotHelper.Snap(window, "01-welcome-initial", "01-welcome");

				// 填表单（TextChanged 处理器会写入设置）
				var userName = UiClick.Find<ForkPlus.UI.Controls.PlaceholderTextBox>(window, "UserNameTextBox");
				var email = UiClick.Find<ForkPlus.UI.Controls.PlaceholderTextBox>(window, "EmailNameTextBox");
				var cloneDir = UiClick.Find<ForkPlus.UI.Controls.PlaceholderTextBox>(window, "DefaultCloneDirectoryTextBox");
				userName.Text = "Test User";
				email.Text = "test@example.com";
				cloneDir.Text = "/tmp/e2e-clone-dir";
				Dispatcher.UIThread.RunJobs();
				ScreenshotHelper.Snap(window, "02-welcome-filled", "01-welcome");

				// 状态断言
				Assert.Equal("Test User", userName.Text);
				Assert.Equal("test@example.com", email.Text);
				window.Close();
			});
		}

		// ============================ RepositoryManagerUserControl ============================

		[Fact]
		public void RepositoryManager_ListsSelectsAndEmptyFallback()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repoA = TestRepoFactory.CreateBasic();
			string repoB = TestRepoFactory.CreateBranches();
			try
			{
				var instance = RepositoryManager.Instance;
				// 快照全局状态，测试后还原
				var prevRepos = instance.Repositories;
				var prevDirs = instance.SourceDirs;

				HeadlessAppBootstrap.Run(delegate
				{
					instance.RemoveAll();
					instance.SetSourceDirs(new string[0]);
					instance.AddRepositories(new[] { repoA, repoB });
					instance.AddOrUpdateLastOpened(repoA); // repoA 进 Recent

					// ===== 1) 有仓库：列表展示 + 分组 =====
					var control = new RepositoryManagerUserControl();
					var window = new ForkPlus.UI.CustomWindow { Width = 1920, Height = 1080, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					int before = ScreenshotHelper.Snap(window, "03-repomanager-with-repos", "01-welcome");

					// 树可见、空态隐藏；Recent + Repositories 分组都渲染出来（本地化：最近/仓库）
					var tree = UiClick.Find<MultiselectionTreeView>(control, "RepositoriesTreeView");
					Assert.True(tree.IsVisible, "有仓库时树应可见");
					Assert.False(UiClick.Find<Grid>(control, "FallbackView").IsVisible, "有仓库时空态视图应隐藏");
					string treeText = TreeText(control);
					Assert.True(treeText.Contains("Recent") || treeText.Contains("最近"), "应包含 Recent 分组，实际: " + treeText);
					Assert.True(treeText.Contains("Repositories") || treeText.Contains("仓库"), "应包含 Repositories 分组，实际: " + treeText);
					Assert.Contains("fpe2e_basic", treeText); // repoA 在 Recent 分组里

					// ===== 2) 点击选中仓库（真点击：压在 TreeViewControlItem 上） =====
					var containers = UiClick.FindAll<TreeViewControlItem>(tree)
						.Where(c => c.Node is RepositoryManagerRepositoryItem)
						.ToList();
					Assert.True(containers.Count >= 1, "应渲染至少 1 个仓库条目（虚拟化可能未全部实现化），实际 " + containers.Count);
					var target = containers.First(c => PathNorm(((RepositoryManagerRepositoryItem)c.Node).Path) == PathNorm(repoA));
					UiClick.Press(target, window, new Avalonia.Point(60, 10));
					Dispatcher.UIThread.RunJobs();
					Assert.NotNull(control.SelectedRepository);
					Assert.Equal(PathNorm(repoA), PathNorm(control.SelectedRepository.Path));
					int after = ScreenshotHelper.Snap(window, "04-repomanager-selected", "01-welcome");
					Assert.True(after > 0, "选中后截图仍应有内容");

					// ===== 3) 清空仓库 → 空态回退视图 =====
					instance.RemoveAll();
					control.Refresh(restoreSelection: false);
					Dispatcher.UIThread.RunJobs();
					ScreenshotHelper.Snap(window, "05-repomanager-empty-fallback", "01-welcome");
					Assert.True(UiClick.Find<Grid>(control, "FallbackView").IsVisible, "无仓库时空态视图应可见");
					Assert.False(tree.IsVisible, "无仓库时树应隐藏");
					window.Close();
				});
				try
				{
					// 还原全局仓库状态（避免污染同进程其他测试）
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
		}

		// ============================ ClosableTabControl ============================

		[Fact]
		public void ClosableTabControl_AddSelectRemove()
		{
			HeadlessAppBootstrap.EnsureStarted();
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
				Assert.Equal(3, tabs.ItemCount);
				ScreenshotHelper.Snap(window, "06-tabs-three", "01-welcome");

				// 选中第三个
				tabs.SelectTab(tab3);
				Dispatcher.UIThread.RunJobs();
				Assert.Same(tab3, tabs.SelectedTab);
				ScreenshotHelper.Snap(window, "07-tabs-third-selected", "01-welcome");

				// 移除当前 tab：选中应移交给相邻 tab
				int removed = 0;
				tabs.TabItemRemoved += delegate { removed++; };
				tabs.RemoveTab(tab3);
				Dispatcher.UIThread.RunJobs();
				Assert.Equal(1, removed);
				Assert.Equal(2, tabs.ItemCount);
				Assert.NotSame(tab3, tabs.SelectedTab);
				ScreenshotHelper.Snap(window, "08-tabs-after-remove", "01-welcome");

				// 上一个/下一个切换
				tabs.SelectNextTab();
				Dispatcher.UIThread.RunJobs();
				var afterNext = tabs.SelectedTab;
				Assert.NotNull(afterNext);
				tabs.SelectPreviousTab();
				Dispatcher.UIThread.RunJobs();
				Assert.NotSame(afterNext, tabs.SelectedTab);
				window.Close();
			});
		}

		// ============================ 工具 ============================

		private static string TreeText(Avalonia.Visual root)
		{
			var sb = new System.Text.StringBuilder();
			foreach (TextBlock tb in root.GetVisualDescendants().OfType<TextBlock>())
			{
				sb.Append(tb.Text).Append('\n');
			}
			return sb.ToString();
		}

		private static string PathNorm(string p)
		{
			return p.Replace('\\', '/').TrimEnd('/');
		}
	}
}
