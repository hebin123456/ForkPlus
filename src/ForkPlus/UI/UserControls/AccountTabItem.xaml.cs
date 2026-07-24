// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Markup → 移除（Avalonia code-behind 不需 IComponentConnector using）
// - using System.Windows.Shapes → using Avalonia.Shapes（Ellipse）
// - RoutedEventArgs → Avalonia.Interactivity.RoutedEventArgs（新增 using Avalonia.Interactivity）
// - Dispatcher.Async 保持（自定义扩展方法 DispatcherExtension.Async，内部转发 Dispatcher.Post）
// - CheckBox.Checked/Unchecked 保持（Avalonia 11.3 ToggleButton 仍提供这两个路由事件）
// TODO(4.5): account.ServiceType.Icon()（RemoteTypeBridgeExtensions.Icon，定义于 BridgeExtensions.cs，尚未迁移）
//            仍返回 WPF System.Windows.Media.ImageSource。Avalonia Image.Source 期望 IImage。
//            待 BridgeExtensions 迁移为返回 Avalonia.Media.IImage 后类型一致（参考 ReferencePanel 对 Remote.Icon 的处理）。
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Shapes;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Utils.Http;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class AccountTabItem : TabItem
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private Account _account;

		private bool _refreshInProgress;

		public event EventHandler<EventArgs<Account>> UpdateTokenButtonClicked;

		public AccountTabItem()
		{
			InitializeComponent();
			NotificationsCheckBox.Checked += NotificationsCheckBox_Checked;
			NotificationsCheckBox.Unchecked += NotificationsCheckBox_Checked;
		}

		public void Refresh(Account account)
		{
			_account = account;
			ServerTypeImage.Source = account.ServiceType.Icon();
			ServerTypeTextBlock.Text = account.ServiceType.FriendlyName();
			UserNameTextBlock.Text = account.Username;
			AuthenticationTypeTextBlock.Text = account.AuthenticationType.FriendlyName();
			if (account.Service is INotificationGitService)
			{
				NotificationsCheckBox.Show();
				_refreshInProgress = true;
				NotificationsCheckBox.IsChecked = account.EnableNotifications;
				_refreshInProgress = false;
			}
			else
			{
				NotificationsCheckBox.Hide();
			}
			StatusTextBlock.Text = Translate("Updating...");
			StatusBusyIndicator.Show();
			StatusEllipse.Hide();
			_jobQueue.Add(Translate("Get user"), delegate
			{
				ServiceResult<User> userResponse = account.Service.GetUser();
				if (!userResponse.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						StatusBusyIndicator.Hide();
						StatusEllipse.Show();
						StatusEllipse.Fill = Theme.ApplicationColors.RedBrush;
						StatusTextBlock.Text = userResponse.Error.FriendlyMessage;
						StatusTextBlock.ToolTip = userResponse.Error.FriendlyMessage;
					});
				}
				else
				{
					base.Dispatcher.Async(delegate
					{
						StatusBusyIndicator.Hide();
						StatusEllipse.Show();
						StatusEllipse.Fill = Theme.ApplicationColors.GreenBrush;
						StatusTextBlock.Text = Translate("Online");
						StatusTextBlock.ToolTip = null;
					});
				}
			});
		}

		private void NotificationsCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			if (!_refreshInProgress)
			{
				Account account = _account;
				if (account != null)
				{
					account.EnableNotifications = NotificationsCheckBox.IsChecked.GetValueOrDefault(true);
					AccountManager.Current.Save();
					NotificationManager.Current.Refresh();
				}
			}
		}

		private void UpdateTokenButton_Click(object sender, RoutedEventArgs e)
		{
			if (_account != null)
			{
				this.UpdateTokenButtonClicked?.Invoke(this, new EventArgs<Account>(_account));
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
