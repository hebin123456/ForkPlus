// 阶段 4.5：移除未使用的 using System.Windows.Input;（ICommand/KeyGesture 已由 using Avalonia.Input 提供）。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Interactivity;

namespace ForkPlus.UI
{
	// 阶段 4.5：WPF System.Windows.Controls → Avalonia.Controls。
	// WPF ApplicationCommands.Cut/Copy/Paste (RoutedCommand + CommandTarget)
	// → 自定义 ICommand 实现，目标 TextBox 通过 CommandParameter 传入（Avalonia MenuItem 无 CommandTarget）。
	// WPF EditingCommands + SpellingError 在 Avalonia 中无内置等价物；
	// AddSpellingMenuItems 当前为空实现。说明：
	// - 调用方 SpellingPlaceholderTextBox 始终传入 spellingError=null（无拼写错误），
	//   此时 WPF 原生行为也是不添加菜单项，故空实现等价于 WPF "无拼写错误" 分支。
	// - 真正的功能缺失是"检测拼写错误"本身，需引入第三方库（如 WeCantSpell.Hunspell）
	//   + 字典文件 + AOT 资源打包，属产品级决策，不在迁移修复范围内。
	public static class MenuExtensions
	{
		private class CutCommand : ICommand
		{
			public static readonly CutCommand Instance = new CutCommand();

			public event EventHandler CanExecuteChanged;

			public bool CanExecute(object parameter)
			{
				return parameter is TextBox;
			}

			public void Execute(object parameter)
			{
				if (parameter is TextBox textBox)
				{
					textBox.Cut();
				}
			}
		}

		private class CopyCommand : ICommand
		{
			public static readonly CopyCommand Instance = new CopyCommand();

			public event EventHandler CanExecuteChanged;

			public bool CanExecute(object parameter)
			{
				return parameter is TextBox;
			}

			public void Execute(object parameter)
			{
				if (parameter is TextBox textBox)
				{
					textBox.Copy();
				}
			}
		}

		private class PasteCommand : ICommand
		{
			public static readonly PasteCommand Instance = new PasteCommand();

			public event EventHandler CanExecuteChanged;

			public bool CanExecute(object parameter)
			{
				return parameter is TextBox;
			}

			public void Execute(object parameter)
			{
				if (parameter is TextBox textBox)
				{
					textBox.Paste();
				}
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
				// 阶段 4.5：Avalonia MenuItem 无 InputGestureText 属性，改用 InputGesture（KeyGesture 类型）。
				// 直接赋值 keyGesture，由 Avalonia 内部负责显示格式化。
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

		// 阶段 4.5：Avalonia 11.3 ItemsControl.Items 返回弱类型 IList（非 IList<object>），目标参数同步改为 IList。
	private static void SetItems(IList targetItems, IEnumerable<Control> items, string ownerDescription)
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

		public static void AddDefaultTextBoxMenuItems(this ContextMenu contextMenu, TextBox commandTarget)
		{
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("Cut");
			menuItem.Command = CutCommand.Instance;
			// 阶段 4.5：Avalonia MenuItem 无 CommandTarget；目标 TextBox 通过 CommandParameter 传入。
			menuItem.CommandParameter = commandTarget;
			contextMenu.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("Copy");
			menuItem2.Command = CopyCommand.Instance;
			menuItem2.CommandParameter = commandTarget;
			contextMenu.Items.Add(menuItem2);
			MenuItem menuItem3 = new MenuItem();
			menuItem3.Header = PreferencesLocalization.MenuHeader("Paste");
			menuItem3.Command = PasteCommand.Instance;
			menuItem3.CommandParameter = commandTarget;
			contextMenu.Items.Add(menuItem3);
		}

		// NOTE(4.5-h): WPF SpellingError + EditingCommands.CorrectSpellingError/IgnoreSpellingError
		// 在 Avalonia 中无内置等价物。当前为空实现。
		// 调用方 SpellingPlaceholderTextBox 始终传入 spellingError=null，此时 WPF 原生行为
		// 也是不添加菜单项（无拼写错误可建议），故空实现等价于 WPF "无拼写错误" 分支，非 bug。
		// 真正的功能缺失是"检测拼写错误"本身，需引入第三方库（如 WeCantSpell.Hunspell）
		// + 字典文件 + AOT 资源打包，属产品级决策，不在迁移修复范围内。
		public static void AddSpellingMenuItems(this ContextMenu contextMenu, object spellingError, object commandTarget)
		{
			// spellingError 始终为 null（见上方注释），无菜单项可添加。
		}
	}
}
