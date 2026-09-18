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

		// v4.1.4（2026-09-18，"检查远端同步状态/跟踪 子菜单点搜索框右键菜单直接消失"）：
		// 搜索框行的 MenuItem 是"叶子"（Items.Count==0）且 StaysOpenOnClick=true——Avalonia 的
		// DefaultMenuInteractionHandler.PointerReleased 会把 Header 里的 TextBox 点击当叶子项点击
		// RaiseClick（WPF 里 TextBox 吞掉鼠标事件不会走到这），本兼容层的叶子关闭处理器此前不看
		// StaysOpenOnClick，一律关整个 ContextMenu。三处关闭入口统一补上该判断：搜索框行/禁用
		// 状态行等"点了不该关"的叶子保持菜单打开（对齐 Avalonia 原生 Click(item) 语义）。
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
					if (menuItem.IsEnabled && menuItem.Items.Count == 0 && !menuItem.StaysOpenOnClick)
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

		/// <summary>子菜单滚动条拖拽守卫（v4.1.4，2026-09-18"子菜单滚动条拉不了，一点就消失"）：
		/// 挂在分组 MenuItem（bubble 路径先于 ContextMenu 层的 DefaultMenuInteractionHandler），
		/// 按住拖拽进行中（指针已被本子菜单内滚动条 Thumb 或搜索框 TextBox 捕获）时标记
		/// PointerMoved 已处理。Avalonia DefaultMenuInteractionHandler.PointerMoved 内部有个
		/// HACK——按住的指针不在菜单项边界内就 Capture(null) 强制松开捕获；子菜单弹层与父
		/// 菜单行跨坐标系，滚动条拖拽的首个 Move 必被误判"越界"，捕获被掐断 → 滚动条拉不动
		/// （headless 实测：按下 captured=Thumb，一次 Move 后 captured=空，滚动停在第一步），
		/// 此后指针带键滑出弹层，子菜单还会因 PointerExited 自关——用户看到"一点就消失"。
		/// Thumb / TextBox 自身的拖拽处理在事件源处先于本守卫执行，不受影响；未按下时无捕获，
		/// 悬停高亮等常规菜单行为也不受影响。</summary>
		public static void AttachSubmenuDragCaptureGuard(this MenuItem menuItem)
		{
			menuItem.AddHandler(InputElement.PointerMovedEvent, SubmenuDragCaptureGuard, RoutingStrategies.Bubble);
		}

		private static void SubmenuDragCaptureGuard(object sender, PointerEventArgs e)
		{
			if (e.Pointer.Captured is global::Avalonia.Visual captured &&
				(captured.GetParent<ScrollBar>() != null || captured.GetParent<TextBox>() != null))
			{
				e.Handled = true;
			}
		}

		/// <summary>搜索框行焦点防抢守卫（v4.1.4，2026-09-18"检查远端同步状态/跟踪 子菜单搜索框
		/// 无法获得焦点/无法输入"）：真实 Windows 桌面实测（SendInput 完整复刻用户流：右键
		/// → 悬停展开 → 键入 → 点击 → 再键入），键盘输入经主窗口原生焦点路由到共享 FocusManager 的逻辑焦点元素——TextBox 逻辑
		/// 聚焦后键入即可达；但两个路径会把焦点抢到搜索框所在的行 MenuItem（叶子项）上：
		/// ① 子菜单打开时 MenuBase 把焦点给第一个子项（= 搜索框行），SubmenuOpened 里
		/// Dispatcher.Post(Background) 的 searchBox.Focus() 早于容器就绪被覆盖；② 点击搜索框
		/// 时 DefaultMenuInteractionHandler.PointerPressed 又把焦点给被按的行 MenuItem。
		/// 守卫：搜索框行自身获得焦点（GotFocus 冒泡到行）且 TextBox 未持焦时，投递
		/// （Background，避开重入）把焦点还给 TextBox；投递回调再校验焦点仍在该行
		/// （用户已把焦点移到分支行/其它元素时不抢回）。分支行等其它项的焦点不受影响。</summary>
		public static void AttachSearchBoxFocusGuard(this MenuItem searchBoxItem, TextBox searchBox)
		{
			// handledEventsToo：MenuItem 的类处理器会把 GotFocus 标记 Handled（真实 Windows
			// 实测：行 IsFocused=True 但普通订阅收不到 GotFocus，LostFocus 不受影响），
			// 必须带 handledEventsToo 才能在"焦点被抢到行上"时收到通知。
			searchBoxItem.AddHandler(InputElement.GotFocusEvent, (_, _) =>
			{
				if (!searchBox.IsFocused)
				{
					searchBoxItem.Dispatcher.Post(delegate
					{
						if (searchBoxItem.IsFocused && !searchBox.IsFocused)
						{
							searchBox.Focus();
						}
					}, DispatcherPriority.Background);
				}
			}, RoutingStrategies.Bubble, handledEventsToo: true);
		}

		private static void AttachCloseOnLeafClick(MenuItem menuItem, ContextMenu contextMenu)
		{
			menuItem.Click += (_, _) =>
			{
				if (menuItem.Items.Count == 0 && !menuItem.StaysOpenOnClick)
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
			if (sender is not MenuItem menuItem || menuItem.Items.Count > 0 || menuItem.StaysOpenOnClick)
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
