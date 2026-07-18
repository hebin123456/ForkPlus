using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
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

		protected override void OnVisualParentChanged(DependencyObject oldParent)
		{
			Log.Info("NotificationManagerUserControl.OnVisualParentChanged()");
			if (_parentPopup != null)
			{
				_parentPopup.Opened -= ParentPopup_Opened;
				_parentPopup.Closed -= ParentPopup_Closed;
			}
			base.OnVisualParentChanged(oldParent);
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
