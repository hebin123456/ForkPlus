// 阶段 4.5：移除未使用的 using System.Windows.Input;（KeyGesture/KeyModifiers/ICommand 已由 using Avalonia.Input 提供）。
using System;
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Commands
{
	// 阶段 4.5：WPF RoutedCommand + CommandBinding + CommandBindings.Add
	// 在 Avalonia 中无直接等价物。
	// - WPF Control.CommandBindings 存储路由命令绑定，由 RoutedCommand 自动查找执行。
	// - Avalonia 通过 InputElement.KeyBindings + KeyGesture + ICommand 实现快捷键。
	//
	// 策略：
	// - CreateMenuItem/CreateMenuItemFormat 保持 Avalonia MenuItem + Click 事件，与 MenuExtensions 一致。
	// - CreateShortcutCommand/CreateShortcutCommandBinding 改为返回 Avalonia KeyBinding，
	//   调用方通过 Control.KeyBindings.Add 注册。
	// - 原 RoutedCommand 自动触发的语义改为调用方显式绑定 KeyGesture 到 ICommand。
	// TODO(4.5-m): SidebarUserControl.xaml.cs 中 CommandBindings.Add 调用需相应改为 KeyBindings.Add。
	internal static class IUICommandExtension
	{
		public static string InputGestureText(this IUICommand command)
		{
			return command.Shortcut?.ToFriendlyString() ?? string.Empty;
		}

		// 阶段 4.5：WPF RoutedCommand + CommandBinding 无 Avalonia 等价物。
		// 返回 Avalonia KeyBinding（KeyGesture + ICommand），调用方添加到 Control.KeyBindings。
		// 调用方签名从 CommandBinding 改为 KeyBinding，需同步迁移 SidebarUserControl 等。
		public static KeyBinding CreateShortcutKeyBinding(this IUICommand command, EventHandler<RoutedEventArgs> handler)
		{
			if (command.Shortcut == null)
			{
				throw new ArgumentException("Cannot create KeyBinding for command without shortcut.");
			}
			// 阶段 4.5：Avalonia KeyBinding 需要 ICommand；用 RelayCommand 包装 handler。
			RelayCommand command2 = new RelayCommand(delegate
			{
				handler(command, new RoutedEventArgs());
			});
			return new KeyBinding
			{
				Gesture = command.Shortcut,
				Command = command2
			};
		}

		// 阶段 4.5：保留旧方法签名以兼容调用方，但实现改为返回 KeyBinding。
		// 调用方需逐步从 CommandBinding 切换到 KeyBinding。
		// 标记 [Obsolete] 提示调用方迁移到 CreateShortcutKeyBinding。
		[Obsolete("Use CreateShortcutKeyBinding instead. WPF CommandBinding has no Avalonia equivalent.")]
		public static KeyBinding CreateShortcutCommandBinding(this IUICommand command, EventHandler<RoutedEventArgs> handler)
		{
			return command.CreateShortcutKeyBinding(handler);
		}

		public static MenuItem CreateMenuItem(this IUICommand command, EventHandler<RoutedEventArgs> clickHandler = null, bool isEnabled = true, bool showShortcut = true)
		{
			return command.CreateMenuItem(command.Title, clickHandler, isEnabled, null, showShortcut);
		}

		public static MenuItem CreateMenuItem(this IUICommand command, Image icon, EventHandler<RoutedEventArgs> clickHandler = null)
		{
			return command.CreateMenuItem(command.Title, clickHandler, isEnabled: true, icon);
		}

		public static MenuItem CreateMenuItem(this IUICommand command, string header, EventHandler<RoutedEventArgs> clickHandler = null, bool isEnabled = true, Image icon = null, bool showShortcut = true)
		{
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader(header);
			if (icon != null)
			{
				menuItem.Icon = CloneIcon(icon);
			}
			menuItem.IsEnabled = isEnabled;
			if (showShortcut)
			{
				menuItem.InputGestureText = command.InputGestureText();
			}
			if (clickHandler != null)
			{
				menuItem.Click += clickHandler;
			}
			return menuItem;
		}

		public static MenuItem CreateMenuItemFormat(this IUICommand command, string header, object[] args, EventHandler<RoutedEventArgs> clickHandler = null, bool isEnabled = true, Image icon = null, bool showShortcut = true)
		{
			MenuItem menuItem = command.CreateMenuItem(header, clickHandler, isEnabled, icon, showShortcut);
			menuItem.Header = PreferencesLocalization.FormatMenuHeader(header, args);
			return menuItem;
		}

		private static Image CloneIcon(Image icon)
		{
			// 阶段 4.5：WPF SnapsToDevicePixels → Avalonia UseLayoutRounding。
			return new Image
			{
				Source = icon.Source,
				Width = icon.Width,
				Height = icon.Height,
				Margin = icon.Margin,
				Stretch = icon.Stretch,
				HorizontalAlignment = icon.HorizontalAlignment,
				VerticalAlignment = icon.VerticalAlignment,
				UseLayoutRounding = icon.UseLayoutRounding
			};
		}

		// 阶段 4.5：简单 ICommand 实现，包装 EventHandler<RoutedEventArgs>。
		// 替代 WPF RoutedCommand 的执行逻辑。
		private class RelayCommand : ICommand
		{
			private readonly Action _execute;

			public RelayCommand(Action execute)
			{
				_execute = execute;
			}

			public event EventHandler CanExecuteChanged;

			public bool CanExecute(object parameter) => true;

			public void Execute(object parameter) => _execute?.Invoke();
		}
	}
}
