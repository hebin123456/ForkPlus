// 诊断复现（2026-09-07，"Linux 外观/工作区按钮无下拉"）：
// 用生产链路（MainWindow → ToolbarUserControl）+ 生产点击序（ToggleButton 先切 IsChecked
// → DropDownButton.OnIsCheckedChanged 开 ContextMenu → 再 raise Click 路由事件 →
// Initialize* 重建菜单项；UiClick.cs 注释实证的生产序，同 E2e07 模式），观察
// ContextMenu 是否打开、项数与弹层渲染。
// 注意：合成 pointer 事件不建立捕获，ToggleButton 的 toggle 不触发（headless 限制），
// 故用 IsChecked= 直接同步 + Click 路由事件补发。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI;
using ForkPlus.UI.UserControls;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ToolbarDropdownDiagnosticTests
	{
		private readonly ITestOutputHelper _output;

		public ToolbarDropdownDiagnosticTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void AppearanceAndWorkspaces_DropdownOpensWithItems()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				MainWindow window = E2eMainWindowHarness.CreateWindow();
				try
				{
					ToolbarUserControl toolbar = window.Toolbar;
					Dispatcher.UIThread.RunJobs();

					Probe(toolbar.AppearanceToolbarDropdownButton, window, "Appearance");
					Probe(toolbar.WorkspacesToolbarDropdownButton, window, "Workspaces");
				}
				finally
				{
					E2eMainWindowHarness.DetachWindow(window);
				}
			});
		}

		private void Probe(global::ForkPlus.UI.Controls.ToolbarDropDownButton button, Window window, string name)
		{
			_output.WriteLine($"[{name}] bounds={button.Bounds}, isEnabled={button.IsEnabled}, contextMenu={(button.ContextMenu != null ? "有" : "无")}");

			// 生产点击序 1：切 IsChecked（→ OnIsCheckedChanged → Post 延迟 Open）
			button.IsChecked = true;

			// 生产点击序 2：同一交互内同步 raise Click 路由事件（→ Initialize* 构建菜单项）。
			// 不得在 1、2 之间跑 RunJobs——生产中 Click 紧随 IsChecked 同步发生，任何 dispatcher
			// 任务都在其后（正是 DropDownButton 延迟 Open 修复依赖的时序）。
			button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

			// dispatcher 执行延迟的 Open（此时菜单项已构建）
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

			ContextMenu menu = button.ContextMenu;
			Assert.NotNull(menu);
			_output.WriteLine($"[{name}] after click-seq: menu.IsOpen={menu.IsOpen}, items={menu.ItemCount}");

			// 弹层视觉树中的 MenuItem：ContextMenu 弹在独立 PopupRoot（非窗口视觉树子孙），
			// 从菜单自身视觉树/VisualRoot 探测（已打开的菜单 attach 到 PopupRoot）。
			Visual? menuRoot = menu as Visual;
			var menuItems = menuRoot.GetVisualDescendants().OfType<MenuItem>().ToList();
			var lastAncestor = menuRoot.GetVisualAncestors().LastOrDefault();
			string rootName = lastAncestor?.GetType().Name ?? "null";
			string detail = string.Join("|", menuItems.Take(5).Select(m => $"{m.Header}({m.Bounds.Width:0.#}x{m.Bounds.Height:0.#})"));
			_output.WriteLine($"[{name}] menu.VisualRoot={rootName}, MenuItems: {menuItems.Count}: {detail}");
			// 回归断言：弹层必须真实渲染出菜单项（旧实现打开空菜单后加项不物化）
			Assert.NotEmpty(menuItems);
			Assert.All(menuItems, m => Assert.True(m.Bounds.Height > 0, "弹层菜单项高度应为正"));

			Assert.True(menu.IsOpen, name + " 菜单应打开");
			Assert.True(menu.ItemCount > 0, name + " 菜单应有项");
			Assert.Equal(true, button.IsChecked);

			// 关闭复位
			menu.Close();
			Dispatcher.UIThread.RunJobs();
			_output.WriteLine($"[{name}] after close: isChecked={button.IsChecked}, menu.IsOpen={menu.IsOpen}");
			Assert.Equal(false, button.IsChecked);
		}
	}
}
