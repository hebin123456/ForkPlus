using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Commands
{
	internal static class IUICommandExtension
	{
		public static string InputGestureText(this IUICommand command)
		{
			return command.Shortcut?.ToFriendlyString() ?? string.Empty;
		}

		public static CommandBinding CreateShortcutCommandBinding(this IUICommand command, ExecutedRoutedEventHandler handler)
		{
			return new CommandBinding(command.CreateShortcutCommand(), handler);
		}

		public static RoutedCommand CreateShortcutCommand(this IUICommand command)
		{
			RoutedCommand routedCommand = new RoutedCommand();
			if (command.Shortcut == null)
			{
				throw new ArgumentException("Cannot create RoutedCommand for command without shortcut.");
			}
			routedCommand.InputGestures.Add(command.Shortcut);
			if (command.Shortcut.Key >= Key.D0 && command.Shortcut.Key <= Key.D9)
			{
				int num = (int)(command.Shortcut.Key - 34);
				Key key = (Key)(74 + num);
				routedCommand.InputGestures.Add(new KeyGesture(key, command.Shortcut.Modifiers));
			}
			if (command.SecondaryShortcut != null)
			{
				routedCommand.InputGestures.Add(command.SecondaryShortcut);
			}
			return routedCommand;
		}

		public static MenuItem CreateMenuItem(this IUICommand command, RoutedEventHandler clickHandler = null, bool isEnabled = true, bool showShortcut = true)
		{
			return command.CreateMenuItem(command.Title, clickHandler, isEnabled, null, showShortcut);
		}

		public static MenuItem CreateMenuItem(this IUICommand command, Image icon, RoutedEventHandler clickHandler = null)
		{
			return command.CreateMenuItem(command.Title, clickHandler, isEnabled: true, icon);
		}

		public static MenuItem CreateMenuItem(this IUICommand command, string header, RoutedEventHandler clickHandler = null, bool isEnabled = true, Image icon = null, bool showShortcut = true)
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

		public static MenuItem CreateMenuItemFormat(this IUICommand command, string header, object[] args, RoutedEventHandler clickHandler = null, bool isEnabled = true, Image icon = null, bool showShortcut = true)
		{
			MenuItem menuItem = command.CreateMenuItem(header, clickHandler, isEnabled, icon, showShortcut);
			menuItem.Header = PreferencesLocalization.FormatMenuHeader(header, args);
			return menuItem;
		}

		private static Image CloneIcon(Image icon)
		{
			return new Image
			{
				Source = icon.Source,
				Width = icon.Width,
				Height = icon.Height,
				Margin = icon.Margin,
				Stretch = icon.Stretch,
				HorizontalAlignment = icon.HorizontalAlignment,
				VerticalAlignment = icon.VerticalAlignment,
				SnapsToDevicePixels = icon.SnapsToDevicePixels
			};
		}
	}
}
