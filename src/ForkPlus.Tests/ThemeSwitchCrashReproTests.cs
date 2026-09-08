// 复现测试（2026-09-07，"切换主题就有可能导致 UI 崩溃，参见外观下拉的按钮和菜单栏
// 窗口里面的按钮"）：真实 MainWindow 生产路径，两个主题切换入口全链路走通：
//   1) 工具栏外观下拉：按钮 Click → InitializeAppearanceToolBarButtonContextMenu 重建
//      → ContextMenu.Open() 打开 → 点击主题菜单项（菜单仍处于打开状态）→
//      SwitchApplicationTheme.Execute → ApplicationThemeChanged → 菜单再次 Items.Clear()
//      重建（此时菜单正开着）→ 菜单关闭路径。
//   2) 菜单栏 Window 菜单：SubmenuOpened → SetItems(CreateWindowMenuItems()) →
//      点击 Switch Theme 二级菜单的主题项。
//   3) Ctrl+点击外观按钮：SwitchApplicationTheme.Execute() 无参 toggle。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ThemeSwitchCrashReproTests
	{
		/// <summary>真实 MainWindow + 真实 Toolbar；挂在窗口上（视觉树完整）。</summary>
		private static ToolbarUserControl GetToolbar(MainWindow window)
		{
			return window.GetVisualDescendants().OfType<ToolbarUserControl>().First();
		}

		// ===== 1) 外观下拉：打开菜单状态下点击主题项（全链路含菜单重建）=====

		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		public void AppearanceDropdown_ClickThemeItem_WhileMenuOpen_NoCrash(int round)
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = E2eMainWindowHarness.CreateWindow();
				try
				{
					var toolbar = GetToolbar(window);
					var button = toolbar.AppearanceToolbarDropdownButton;
					Assert.NotNull(button);
					Assert.NotNull(button.ContextMenu);

					for (int i = 0; i < round; i++)
					{
						// 与生产一致：按钮 Click → 重建菜单（InitializeAppearanceToolBarButtonContextMenu）
						toolbar.AppearanceToolbarDropdownButton.RaiseEvent(
							new RoutedEventArgs(Button.ClickEvent));
						Dispatcher.UIThread.RunJobs();

						// 打开菜单（与 DropDownButton.OpenDropdown 一致：IsChecked=true → OpenDropdown）
						button.ContextMenu.PlacementTarget = button;
						button.ContextMenu.MinWidth = 100;
						button.ContextMenu.Open();
						Dispatcher.UIThread.RunJobs();
						bool menuIsOpen = button.ContextMenu.IsOpen;
						Assert.True(menuIsOpen, "外观下拉菜单应处于打开状态");

						// 找到第一个主题菜单项（非 Header、非 Separator 的 MenuItem）
						var themeItem = button.ContextMenu.Items
							.OfType<MenuItem>()
							.FirstOrDefault(m => m.Header != null && m.IsEnabled && m.Items.Count == 0);
						Assert.NotNull(themeItem);

						// 菜单打开状态下点击主题项：Click → Execute → ApplicationThemeChanged
						// → ToolbarUserControl.ApplicationThemeChanged → 菜单 Items.Clear() 重建（菜单还开着！）
						themeItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
						Dispatcher.UIThread.RunJobs();

						// 关闭菜单（AttachCloseOnLeafItemClick 会 Post 关闭；这里显式关闭走完整路径）
						button.ContextMenu.Close();
						Dispatcher.UIThread.RunJobs();
					}

					// 恢复主题
					MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Light);
					Dispatcher.UIThread.RunJobs();
					return true;
				}
				finally
				{
					E2eMainWindowHarness.DetachWindow(window);
				}
			});
		}

		// ===== 2) 菜单栏 Window 菜单：打开子菜单后点击 Switch Theme 主题项 =====

		[Fact]
		public void WindowMenu_ClickThemeItem_WhileSubmenuOpen_NoCrash()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = E2eMainWindowHarness.CreateWindow();
				try
				{
					var mainMenu = window.GetVisualDescendants().OfType<Menu>().FirstOrDefault(m => m.Name == "MainMenu")
						?? window.GetVisualDescendants().OfType<Menu>().First();
					// 菜单栏 Header 走本地化（MainWindowMenuManager：PreferencesLocalization.Translate
					// ("_Window", UiLanguage)）——经 Tr 对齐当前语言后匹配。
					string windowMenuHeader = E2eMainWindowHarness.Tr("_Window");
					var windowMenuItem = mainMenu.Items.OfType<MenuItem>()
						.FirstOrDefault(m => string.Equals(m.Header?.ToString(), windowMenuHeader, StringComparison.Ordinal));
					Assert.NotNull(windowMenuItem);

					// 与生产一致：SubmenuOpened 触发 SetItems(CreateWindowMenuItems())
					windowMenuItem.IsSubMenuOpen = true;
					Dispatcher.UIThread.RunJobs();

					// 找到 Switch Theme 父项（同样本地化）
					string switchThemeHeader = E2eMainWindowHarness.Tr("Switch Theme");
					var switchThemeParent = windowMenuItem.Items.OfType<MenuItem>()
						.FirstOrDefault(m => string.Equals(m.Header?.ToString(), switchThemeHeader, StringComparison.Ordinal));
					Assert.NotNull(switchThemeParent);

					// 展开二级菜单（模拟悬停展开）
					switchThemeParent.IsSubMenuOpen = true;
					Dispatcher.UIThread.RunJobs();

					// 点击其中一个主题项
					var themeItem = switchThemeParent.Items.OfType<MenuItem>()
						.FirstOrDefault(m => m.Items.Count == 0 && m.IsEnabled);
					Assert.NotNull(themeItem);
					themeItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
					Dispatcher.UIThread.RunJobs();

					// 关闭菜单链
					switchThemeParent.IsSubMenuOpen = false;
					windowMenuItem.IsSubMenuOpen = false;
					Dispatcher.UIThread.RunJobs();

					// 恢复主题
					MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Light);
					Dispatcher.UIThread.RunJobs();
					return true;
				}
				finally
				{
					E2eMainWindowHarness.DetachWindow(window);
				}
			});
		}

		// ===== 3) Ctrl+点击外观按钮：无参 toggle 切换 =====

		[Fact]
		public void AppearanceButton_CtrlClick_TogglesTheme_NoCrash()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = E2eMainWindowHarness.CreateWindow();
				try
				{
					var toolbar = GetToolbar(window);
					var button = toolbar.AppearanceToolbarDropdownButton;

					ForkPlusSettings.Default.Theme = ThemeType.Light;
					Dispatcher.UIThread.RunJobs();

					// 直接走 Click 处理器里的无参 Execute 路径（Ctrl 场景）
					MainWindow.Commands.SwitchApplicationTheme.Execute();
					Dispatcher.UIThread.RunJobs();

					// 再切回（Dark → Light）
					MainWindow.Commands.SwitchApplicationTheme.Execute();
					Dispatcher.UIThread.RunJobs();

					// 菜单重建不应崩溃，且按钮/菜单仍可用
					toolbar.AppearanceToolbarDropdownButton.RaiseEvent(
						new RoutedEventArgs(Button.ClickEvent));
					Dispatcher.UIThread.RunJobs();
					Assert.NotNull(button.ContextMenu);
					Assert.True(button.ContextMenu.Items.Count > 0, "外观菜单应被重建且非空");
					return true;
				}
				finally
				{
					E2eMainWindowHarness.DetachWindow(window);
				}
			});
		}
	}
}
