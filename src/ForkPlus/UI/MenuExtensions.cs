using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	public static class MenuExtensions
	{
		private class PasteCommand : ICommand
		{
			public static readonly PasteCommand Instance = new PasteCommand();

			public event EventHandler CanExecuteChanged;

			public bool CanExecute(object parameter)
			{
				return true;
			}

			public void Execute(object parameter)
			{
				ApplicationCommands.Paste.Execute(parameter, Keyboard.FocusedElement);
			}
		}

		public static void SetItems(this ContextMenu menu, IEnumerable<Control> items)
		{
			SetItems(menu.Items, items, VisualTreeAttachmentHelper.Describe(menu));
		}

		public static void SetItems(this MenuItem menu, IEnumerable<Control> items)
		{
			SetItems(menu.Items, items, VisualTreeAttachmentHelper.Describe(menu));
		}

		public static MenuItem AddMenuItem(this MenuBase menu, string header, [Null] RoutedEventHandler clickHandler = null, [Null] Image icon = null, [Null] KeyGesture keyGesture = null, bool isEnabled = true)
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
				menuItem.InputGestureText = keyGesture.ToFriendlyString();
			}
			if (clickHandler != null)
			{
				menuItem.Click += clickHandler;
			}
			menu.Items.Add(menuItem);
			return menuItem;
		}

		public static MenuItem AddMenuItemFormat(this MenuBase menu, string header, object[] args, [Null] RoutedEventHandler clickHandler = null, [Null] Image icon = null, [Null] KeyGesture keyGesture = null, bool isEnabled = true)
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
				VerticalAlignment = icon.VerticalAlignment,
				SnapsToDevicePixels = icon.SnapsToDevicePixels
			};
		}

		private static void SetItems(ItemCollection targetItems, IEnumerable<Control> items, string ownerDescription)
		{
			targetItems.Clear();
			HashSet<Control> hashSet = new HashSet<Control>();
			foreach (Control item in items ?? Array.Empty<Control>())
			{
				Control control = PrepareMenuControl(item, hashSet, ownerDescription);
				if (control == null)
				{
					continue;
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
			return item;
		}

		public static void AddDefaultTextBoxMenuItems(this ContextMenu contextMenu, IInputElement commandTarget)
		{
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("Cut");
			menuItem.Command = ApplicationCommands.Cut;
			menuItem.CommandTarget = commandTarget;
			contextMenu.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("Copy");
			menuItem2.Command = ApplicationCommands.Copy;
			menuItem2.CommandTarget = commandTarget;
			contextMenu.Items.Add(menuItem2);
			MenuItem menuItem3 = new MenuItem();
			menuItem3.Header = PreferencesLocalization.MenuHeader("Paste");
			menuItem3.Command = PasteCommand.Instance;
			menuItem3.CommandTarget = commandTarget;
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
				menuItem.CommandTarget = commandTarget;
				contextMenu.Items.Insert(num, menuItem);
				num++;
			}
			contextMenu.Items.Insert(num, new Separator());
			num++;
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("Ignore All");
			menuItem2.Command = EditingCommands.IgnoreSpellingError;
			menuItem2.CommandTarget = commandTarget;
			contextMenu.Items.Insert(num, menuItem2);
			if (!flag)
			{
				num++;
				contextMenu.Items.Insert(num, new Separator());
			}
		}
	}
}
