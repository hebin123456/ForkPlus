using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;
using ForkPlus.UI.WpfCompat;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace ForkPlus.UI
{
	public static class MenuExtensions
	{
		private class PasteCommand : global::System.Windows.Input.ICommand
		{
			public static readonly PasteCommand Instance = new PasteCommand();

			public event EventHandler CanExecuteChanged;

			public bool CanExecute(object parameter)
			{
				return true;
			}

			public void Execute(object parameter)
			{
				ApplicationCommands.Paste.Execute(parameter ?? Keyboard.FocusedElement);
			}
		}

		public static void SetItems(this ContextMenu menu, IEnumerable<Control> items)
		{
			ContextMenuCompat.AttachAutoDismiss(menu, menu.PlacementTarget as Control);
			SetItems(menu.Items, items, VisualTreeAttachmentHelper.Describe(menu));
			menu.AttachCloseOnLeafItemClick();
		}

		public static void SetItems(this MenuItem menu, IEnumerable<Control> items)
		{
			SetItems(menu.Items, items, VisualTreeAttachmentHelper.Describe(menu));
		}

		public static void AttachCloseOnLeafItemClick(this ContextMenu menu)
		{
			if (menu == null)
			{
				return;
			}

			menu.RemoveHandler(InputElement.PointerReleasedEvent, ContextMenu_PointerReleasedCloseLeafItem);
			menu.AddHandler(
				InputElement.PointerReleasedEvent,
				ContextMenu_PointerReleasedCloseLeafItem,
				RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
				handledEventsToo: true);

			foreach (object item in menu.Items)
			{
				if (item is MenuItem menuItem)
				{
					AttachCloseOnLeafClick(menuItem, menu);
				}
			}
		}

		private static void ContextMenu_PointerReleasedCloseLeafItem(object sender, PointerReleasedEventArgs e)
		{
			if (sender is not ContextMenu contextMenu)
			{
				return;
			}
			if (e.InitialPressMouseButton != MouseButton.Left)
			{
				return;
			}

			for (StyledElement current = e.Source as StyledElement; current != null; current = current.Parent as StyledElement)
			{
				if (current is TextBox)
				{
					return;
				}
				if (current is MenuItem menuItem)
				{
					if (menuItem.IsEnabled && menuItem.Items.Count == 0)
					{
						Dispatcher.UIThread.Post(contextMenu.Close, DispatcherPriority.Background);
					}
					return;
				}
			}
		}

		public static MenuItem AddMenuItem(this MenuBase menu, string header, [Null] EventHandler<RoutedEventArgs> clickHandler = null, [Null] Image icon = null, [Null] KeyGesture keyGesture = null, bool isEnabled = true)
		{
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader(header);
			if (icon != null)
			{
				menuItem.Icon = CloneIcon(icon);
			}
			menuItem.IsEnabled = isEnabled;
			if (keyGesture != null)
			{
				// Migration note：Avalonia MenuItem 无 InputGestureText 字符串属性，改为设置 InputGesture(KeyGesture)。
				menuItem.InputGesture = keyGesture;
			}
			if (clickHandler != null)
			{
				menuItem.Click += clickHandler;
			}
			menu.Items.Add(menuItem);
			return menuItem;
		}

		public static MenuItem AddMenuItemFormat(this MenuBase menu, string header, object[] args, [Null] EventHandler<RoutedEventArgs> clickHandler = null, [Null] Image icon = null, [Null] KeyGesture keyGesture = null, bool isEnabled = true)
		{
			MenuItem menuItem = AddMenuItem(menu, header, clickHandler, icon, keyGesture, isEnabled);
			menuItem.Header = PreferencesLocalization.FormatMenuHeader(header, args);
			return menuItem;
		}

		private static void TranslateMenuControl(Control control)
		{
			if (control is MenuItem menuItem && menuItem.Header is string header)
			{
				menuItem.Header = PreferencesLocalization.MenuHeader(header.Replace("__", "_"));
			}
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
				VerticalAlignment = icon.VerticalAlignment
			};
		}

		private static void SetItems(ItemCollection targetItems, IEnumerable<Control> items, string ownerDescription)
		{
			targetItems.Clear();
			HashSet<Control> hashSet = new HashSet<Control>();
			bool previousWasSeparator = true;
			foreach (Control item in items ?? Array.Empty<Control>())
			{
				Control control = PrepareMenuControl(item, hashSet, ownerDescription);
				if (control == null)
				{
					continue;
				}
				if (control is Separator)
				{
					if (previousWasSeparator)
					{
						continue;
					}
					ApplySeparatorTheme(control);
					previousWasSeparator = true;
				}
				else
				{
					previousWasSeparator = false;
				}
				TranslateMenuControl(control);
				try
				{
					targetItems.Add(control);
				}
				catch (ArgumentException ex)
				{
					Log.Warn("Skipping " + VisualTreeAttachmentHelper.Describe(control) + " while rebuilding " + ownerDescription + ". " + ex.Message, ex);
				}
			}
			while (targetItems.Count > 0 && targetItems[targetItems.Count - 1] is Separator)
			{
				targetItems.RemoveAt(targetItems.Count - 1);
			}
		}

		private static Control PrepareMenuControl([Null] Control item, HashSet<Control> seenItems, string ownerDescription)
		{
			if (item == null)
			{
				return null;
			}
			if (!seenItems.Add(item))
			{
				if (item is Separator)
				{
					return new Separator();
				}
				Log.Warn("Skipping duplicate menu control " + VisualTreeAttachmentHelper.Describe(item) + " while rebuilding " + ownerDescription + ".");
				return null;
			}
			if (!VisualTreeAttachmentHelper.PrepareForNewParent(item, ownerDescription))
			{
				Log.Warn("Skipping still-parented menu control " + VisualTreeAttachmentHelper.Describe(item) + " while rebuilding " + ownerDescription + ".");
				return null;
			}
			if (item is MenuItem menuItem)
			{
				AttachCloseOnLeafClick(menuItem);
			}
			return item;
		}

		private static void AttachCloseOnLeafClick(MenuItem menuItem)
		{
			menuItem.Click -= MenuItem_CloseOwningMenuOnClick;
			menuItem.Click += MenuItem_CloseOwningMenuOnClick;
			foreach (object item in menuItem.Items)
			{
				if (item is MenuItem childMenuItem)
				{
					AttachCloseOnLeafClick(childMenuItem);
				}
			}
		}

		private static void AttachCloseOnLeafClick(MenuItem menuItem, ContextMenu contextMenu)
		{
			menuItem.Click += (_, _) =>
			{
				if (menuItem.Items.Count == 0)
				{
					Dispatcher.UIThread.Post(contextMenu.Close, DispatcherPriority.Background);
				}
			};
			foreach (object item in menuItem.Items)
			{
				if (item is MenuItem childMenuItem)
				{
					AttachCloseOnLeafClick(childMenuItem, contextMenu);
				}
			}
		}

		private static void MenuItem_CloseOwningMenuOnClick(object sender, RoutedEventArgs e)
		{
			if (sender is not MenuItem menuItem || menuItem.Items.Count > 0)
			{
				return;
			}

			Dispatcher.UIThread.Post(() => CloseOwningMenu(menuItem), DispatcherPriority.Background);
		}

		private static void CloseOwningMenu(MenuItem menuItem)
		{
			for (StyledElement current = menuItem; current != null; current = current.Parent as StyledElement)
			{
				if (current is ContextMenu contextMenu)
				{
					contextMenu.Close();
					return;
				}
				if (current is MenuItem parentMenuItem)
				{
					parentMenuItem.IsSubMenuOpen = false;
				}
			}
		}

		private static void ApplySeparatorTheme(Control control)
		{
			if (control is TemplatedControl templatedControl &&
				Application.Current?.TryFindResource("SeparatorStyleKey", out var style) == true &&
				style is ControlTheme theme)
			{
				templatedControl.Theme = theme;
			}
		}

		public static void AddDefaultTextBoxMenuItems(this ContextMenu contextMenu, IInputElement commandTarget)
		{
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("Cut");
			menuItem.Command = ApplicationCommands.Cut;
			menuItem.CommandParameter = commandTarget;
			contextMenu.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("Copy");
			menuItem2.Command = ApplicationCommands.Copy;
			menuItem2.CommandParameter = commandTarget;
			contextMenu.Items.Add(menuItem2);
			MenuItem menuItem3 = new MenuItem();
			menuItem3.Header = PreferencesLocalization.MenuHeader("Paste");
			menuItem3.Command = PasteCommand.Instance;
			menuItem3.CommandParameter = commandTarget;
			contextMenu.Items.Add(menuItem3);
		}

		public static void AddSpellingMenuItems(this ContextMenu contextMenu, SpellingError spellingError, IInputElement commandTarget)
		{
			if (spellingError == null)
			{
				return;
			}
			bool flag = contextMenu.Items.Count == 0;
			int num = 0;
			foreach (string suggestion in spellingError.Suggestions)
			{
				MenuItem menuItem = new MenuItem();
				menuItem.Header = suggestion;
				menuItem.FontWeight = FontWeights.Bold;
				menuItem.Command = EditingCommands.CorrectSpellingError;
				menuItem.CommandParameter = suggestion;
				contextMenu.Items.Insert(num, menuItem);
				num++;
			}
			contextMenu.Items.Insert(num, new Separator());
			num++;
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("Ignore All");
			menuItem2.Command = EditingCommands.IgnoreSpellingError;
			contextMenu.Items.Insert(num, menuItem2);
			if (!flag)
			{
				num++;
				contextMenu.Items.Insert(num, new Separator());
			}
		}
	}
}
