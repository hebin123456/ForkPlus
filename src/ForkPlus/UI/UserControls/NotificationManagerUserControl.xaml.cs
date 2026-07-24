// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl/ListBox）
// - using System.Windows.Controls.Primitives → using Avalonia.Controls.Primitives（ToggleButton/Popup）
// - using System.Windows.Markup → 移除
// - 新增 using Avalonia.VisualTree（OnAttachedToVisualTree/VisualTreeAttachmentEventArgs）
// - OnVisualParentChanged(DependencyObject oldParent) → OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
//   （Avalonia 无 OnVisualParentChanged；控件加入视觉树时回调 OnAttachedToVisualTree，参考 Avalonia Control）
// - DependencyObject → AvaloniaObject（VisualTreeAttachmentHelper）
// - this.Parent<Popup>() → 不变（GetParent<T> 扩展方法已迁移为 AvaloniaObject，参考 DependencyObjectExtensions）
// - base.IsVisible → Visual.IsVisible（Avalonia 同名属性）
// - SelectionChangedEventArgs → Avalonia.Controls 同名类型
// - Popup.Opened/Closed、ToggleButton.IsChecked、Popup.PlacementTarget API 兼容
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ForkPlus.Accounts;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Helpers;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	/// <summary>右上角通知按钮的弹出面板。
	/// 实现 ILocalizableControl：MainWindow.ApplyLocalization 时会回调本类的
	/// ApplyLocalization() 重新翻译 HeaderLabel.Text，避免语言切换后弹出面板
	/// 仍显示旧语言（Bug v2.1.2：通知按钮切换语言不实时刷新）。</summary>
	public partial class NotificationManagerUserControl : UserControl, ILocalizableControl
	{
		private readonly ObservableCollection<NotificationViewModel> _notifications = new ObservableCollection<NotificationViewModel>();

		private Popup _parentPopup;

		private ToggleButton _parentButton;

		public NotificationManagerUserControl()
		{
			InitializeComponent();
			HeaderLabel.Text = PreferencesLocalization.Translate("Notifications", ForkPlusSettings.Default.UiLanguage);
			ListBox.ItemsSource = _notifications;
			NotificationManager.Current.IsUpdatingChanged += NotificationManager_IsUpdatingChanged;
			NotificationManager.Current.NotificationsChanged += NotificationManager_NotificationsChanged;
			RefreshButton.Click += delegate
			{
				NotificationManager.Current.Refresh();
			};
		}

		/// <summary>MainWindow.ApplyLocalization 回调入口。
		/// HeaderLabel.Text 在构造函数里只设一次，之前语言切换后弹出面板仍显示旧语言，
		/// 需重启客户端才生效。这里按当前 UiLanguage 重新翻译。</summary>
		public void ApplyLocalization()
		{
			HeaderLabel.Text = PreferencesLocalization.Translate("Notifications", ForkPlusSettings.Default.UiLanguage);
		}

		// 阶段 4.5：WPF OnVisualParentChanged(DependencyObject oldParent)
		// → Avalonia OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)。
		// WPF 在视觉父级变化时回调（含挂载/卸载），Avalonia 在控件加入视觉树时回调 OnAttachedToVisualTree。
		// 语义等价：控件被放入 Popup 后，沿视觉树向上查找父级 Popup 并订阅其 Opened/Closed。
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			Log.Info("NotificationManagerUserControl.OnAttachedToVisualTree()");
			base.OnAttachedToVisualTree(e);
			if (_parentPopup != null)
			{
				_parentPopup.Opened -= ParentPopup_Opened;
				_parentPopup.Closed -= ParentPopup_Closed;
			}
			// 阶段 4.5：this.Parent<Popup>() 使用 GetParent<T> 扩展方法（参考 DependencyObjectExtensions）。
			_parentPopup = this.Parent<Popup>();
			_parentButton = _parentPopup?.PlacementTarget as ToggleButton;
			if (_parentPopup == null || _parentButton == null)
			{
				return;
			}
			_parentPopup.Opened += ParentPopup_Opened;
			_parentPopup.Closed += ParentPopup_Closed;
		}

		public void ShowPopup()
		{
			if (_parentButton != null)
			{
				_parentButton.IsChecked = true;
			}
		}

		public void HidePopup()
		{
			if (_parentButton != null)
			{
				_parentButton.IsChecked = false;
			}
		}

		private void ParentPopup_Opened(object sender, EventArgs e)
		{
			Log.Info("_parentPopup_Opened");
			_parentButton.Disable();
			Opened();
		}

		private void ParentPopup_Closed(object sender, EventArgs e)
		{
			Log.Info("_parentPopup_Closed");
			_parentButton.Enable();
			Closed();
		}

		private void Opened()
		{
			Log.Info("NotificationManagerUserControl.Opened()");
			RefreshBusyIndicator();
			RefreshNotifications();
		}

		private void Closed()
		{
			Log.Info("NotificationManagerUserControl.Closed()");
		}

		private void NotificationManager_IsUpdatingChanged(object sender, EventArgs e)
		{
			// 阶段 4.5：WPF base.IsVisible → Avalonia Visual.IsVisible（同名属性）。
			if (base.IsVisible)
			{
				RefreshBusyIndicator();
			}
		}

		private void NotificationManager_NotificationsChanged(object sender, EventArgs e)
		{
			if (base.IsVisible)
			{
				RefreshNotifications();
			}
		}

		private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count != 0 && ListBox.SelectedItem is NotificationViewModel notificationViewModel)
			{
				Log.Info("NotificationManagerUserControl.ListBox_SelectionChanged()");
				NotificationManager.Current.UnsetUnread(notificationViewModel.Notification);
				new Uri(notificationViewModel.TargetUrl).OpenInBrowser();
				HidePopup();
				_notifications.Clear();
			}
		}

		private void RefreshBusyIndicator()
		{
			if (NotificationManager.Current.IsUpdating)
			{
				BusyIndicator.Show();
				RefreshButton.Hide();
			}
			else
			{
				BusyIndicator.Hide();
				RefreshButton.Show();
			}
		}

		private void RefreshNotifications()
		{
			NotificationViewModel[] array = NotificationManager.Current.Notifications.Map((GitServiceNotification x) => new NotificationViewModel(x));
			_notifications.Clear();
			NotificationViewModel[] array2 = array;
			foreach (NotificationViewModel item in array2)
			{
				_notifications.Add(item);
			}
		}

	}
}
