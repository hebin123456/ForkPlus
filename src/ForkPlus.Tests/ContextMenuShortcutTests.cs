// 右键菜单快捷键专项测试（2026-09-11 修复，"右键菜单的快捷键都用不了"）：
// 根因：菜单项的 InputGesture（CreateMenuItem 从 command.Shortcut 设置）在 Avalonia 仅显示
// 不响应按键；菜单打开时按键或路由进弹层（焦点在弹层）、或路由进宿主窗口（焦点未离开），
// 两条路径此前都没人匹配手势 → 分支右键 Delete/Del、各菜单 Ctrl+C 等全部无效。
// 修复：ContextMenuCompat.AttachAutoDismiss 双路接管——ContextMenu 自身 Tunnel KeyDown
// （弹层路径）+ AutoDismissState.AttachForOpen 挂宿主窗口 Tunnel KeyDown（窗口路径），
// 共用 OnKeyDownMatchGesture：命中启用叶子项的 (Key, KeyModifiers) 即触发 Click、
// 收起菜单并置 Handled（先于窗口级 CommandBinding，同手势不双触发）。
// 本文件验证两条路由路径 + 修饰键匹配 + 无匹配/禁用/子菜单场景 + 不吃无关按键。
// 菜单项用生产链路构造：IUICommandExtension.CreateMenuItem（InputGesture 即 command.Shortcut）。
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.UI;
using ForkPlus.UI.Commands;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ContextMenuShortcutTests
	{
		private static void RunJobs()
		{
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>生产链路构造手势菜单项：ShowRemoveLocalBranchWindow.Shortcut = Key.Delete（与侧栏分支菜单 Delete 项一致）。</summary>
		private static MenuItem NewDeleteItem(Action onInvoke, bool isEnabled = true)
		{
			return new ShowRemoveLocalBranchWindowCommand().CreateMenuItem("Delete 'feature/x'...", delegate
			{
				onInvoke();
			}, isEnabled);
		}

		/// <summary>装配：窗口 + 挂 ContextMenu 的按钮 + SetItems（生产入口，内部走 AttachAutoDismiss）。</summary>
		private static ContextMenu SetupMenu(Window window, Button button, List<Control> items)
		{
			var menu = new ContextMenu();
			button.ContextMenu = menu;
			menu.PlacementTarget = button;
			menu.SetItems(items);
			return menu;
		}

		// ===== 1) 弹层路径：菜单打开时按键路由进 ContextMenu 自身 =====

		[Fact]
		public void GestureKey_OnContextMenuWhileOpen_InvokesItemAndClosesMenu()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 300, Height = 200 };
				window.Show();
				var button = new Button { Content = "b" };
				window.Content = button;
				RunJobs();

				bool invoked = false;
				var menu = SetupMenu(window, button, new List<Control>
				{
					NewDeleteItem(delegate { invoked = true; })
				});
				menu.Open(button);
				RunJobs();
				Assert.True(menu.IsOpen);

				// 弹层路径：焦点在弹层内，KeyDown 沿弹层视觉树路由，ContextMenu 自身 Tunnel 处理器接管
				menu.RaiseEvent(new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.Delete,
					KeyModifiers = KeyModifiers.None
				});
				RunJobs();

				Assert.True(invoked, "菜单打开时按下 Delete（菜单项显示的 Del 手势）应触发该项");
				Assert.False(menu.IsOpen, "手势触发后菜单应收起");
			});
		}

		// ===== 2) 窗口路径：菜单打开时按键路由进宿主窗口（焦点未离开主窗口） =====

		[Fact]
		public void GestureKey_OnOwnerWindowWhileMenuOpen_InvokesItemAndClosesMenu()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 300, Height = 200 };
				window.Show();
				var button = new Button { Content = "b" };
				window.Content = button;
				RunJobs();

				bool invoked = false;
				var menu = SetupMenu(window, button, new List<Control>
				{
					NewDeleteItem(delegate { invoked = true; })
				});
				menu.Open(button);
				RunJobs(); // Opened → Post(AttachForOpen) → 窗口级 Tunnel KeyDown 已装好
				Assert.True(menu.IsOpen);

				KeyEventArgs args = new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.Delete,
					KeyModifiers = KeyModifiers.None
				};
				window.RaiseEvent(args);
				RunJobs();

				Assert.True(invoked, "焦点在主窗口时按下 Delete 也应触发打开菜单中的对应项");
				Assert.True(args.Handled, "命中的手势按键应被吞掉（不再落到窗口级 CommandBinding，防双触发）");
				Assert.False(menu.IsOpen, "手势触发后菜单应收起");
			});
		}

		// ===== 3) 修饰键精确匹配 + 无匹配不吃事件 =====

		[Fact]
		public void GestureKey_ModifiersMustMatch_PlainKeyDoesNotInvoke()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 300, Height = 200 };
				window.Show();
				var button = new Button { Content = "b" };
				window.Content = button;
				RunJobs();

				bool invoked = false;
				// Ctrl+S 手势（生产构造：命令 Shortcut 直接决定 InputGesture）
				MenuItem saveItem = new ShowQuickPushWindowCommandStub().CreateMenuItem("Push...", delegate
				{
					invoked = true;
				});
				saveItem.InputGesture = new KeyGesture(Key.S, KeyModifiers.Control);
				var menu = SetupMenu(window, button, new List<Control> { saveItem });
				menu.Open(button);
				RunJobs();

				// 不带 Ctrl 的 S：不匹配
				menu.RaiseEvent(new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.S,
					KeyModifiers = KeyModifiers.None
				});
				RunJobs();
				Assert.False(invoked, "缺修饰键的手势不应触发");
				Assert.True(menu.IsOpen, "无匹配时菜单不应被误关");

				// Ctrl+S：匹配
				KeyEventArgs matched = new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.S,
					KeyModifiers = KeyModifiers.Control
				};
				menu.RaiseEvent(matched);
				RunJobs();
				Assert.True(invoked, "Ctrl+S 应触发菜单项");
				Assert.True(matched.Handled);
			});
		}

		// ===== 4) 禁用项不触发、事件放行 =====

		[Fact]
		public void GestureKey_DisabledItem_DoesNotInvoke_DoesNotSwallowKey()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 300, Height = 200 };
				window.Show();
				var button = new Button { Content = "b" };
				window.Content = button;
				RunJobs();

				bool invoked = false;
				var menu = SetupMenu(window, button, new List<Control>
				{
					NewDeleteItem(delegate { invoked = true; }, isEnabled: false)
				});
				menu.Open(button);
				RunJobs();

				KeyEventArgs args = new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.Delete,
					KeyModifiers = KeyModifiers.None
				};
				menu.RaiseEvent(args);
				RunJobs();

				Assert.False(invoked, "禁用项的手势不应触发");
				Assert.False(args.Handled, "禁用项不匹配时按键应放行（其他处理器仍可响应）");
				Assert.True(menu.IsOpen, "菜单不应被误关");
			});
		}

		// ===== 5) 子菜单递归匹配 =====

		[Fact]
		public void GestureKey_NestedSubmenuItem_IsFound()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 300, Height = 200 };
				window.Show();
				var button = new Button { Content = "b" };
				window.Content = button;
				RunJobs();

				bool invoked = false;
				var parent = new MenuItem { Header = "Advanced" };
				parent.Items.Add(NewDeleteItem(delegate { invoked = true; }));
				var menu = SetupMenu(window, button, new List<Control>
				{
					new MenuItem { Header = "Plain item" },
					parent
				});
				menu.Open(button);
				RunJobs();

				menu.RaiseEvent(new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.Delete,
					KeyModifiers = KeyModifiers.None
				});
				RunJobs();

				Assert.True(invoked, "子菜单里的手势项也应被匹配触发");
				Assert.False(menu.IsOpen);
			});
		}

		// ===== 6) 菜单关闭后手势处理器不残留（不劫持窗口正常按键） =====

		[Fact]
		public void GestureKey_AfterMenuClosed_DoesNotInterceptWindowKeys()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 300, Height = 200 };
				window.Show();
				var button = new Button { Content = "b" };
				window.Content = button;
				RunJobs();

				bool invoked = false;
				var menu = SetupMenu(window, button, new List<Control>
				{
					NewDeleteItem(delegate { invoked = true; })
				});
				menu.Open(button);
				RunJobs();
				menu.Close();
				RunJobs(); // Closed → Detach：窗口级处理器应已卸载

				KeyEventArgs args = new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.Delete,
					KeyModifiers = KeyModifiers.None
				};
				window.RaiseEvent(args);
				RunJobs();

				Assert.False(invoked, "菜单已关闭后，同样的按键不应再触发菜单项");
				Assert.False(args.Handled, "关闭后按键不应被吞");
			});
		}

		/// <summary>仅承载 CreateMenuItem 生产构造（标题/手势从命令来），不执行真实 Push。</summary>
		private sealed class ShowQuickPushWindowCommandStub : IUICommand
		{
			public string Title => "Push";

			public KeyGesture Shortcut { get; } = new KeyGesture(Key.P, KeyModifiers.Control | KeyModifiers.Shift);

			public KeyGesture SecondaryShortcut => null;
		}
	}
}
