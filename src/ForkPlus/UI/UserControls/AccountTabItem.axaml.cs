using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using Avalonia.Controls.Shapes;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Utils.Http;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

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
			NotificationsCheckBox.IsCheckedChanged+=NotificationsCheckBox_Checked;
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
					base.Dispatcher.Post(delegate
					{
						StatusBusyIndicator.Hide();
						StatusEllipse.Show();
						StatusEllipse.Fill = global::ForkPlus.UI.Theme.ApplicationColors.RedBrush;
						StatusTextBlock.Text = userResponse.Error.FriendlyMessage;
						global::Avalonia.Controls.ToolTip.SetTip(StatusTextBlock,userResponse.Error.FriendlyMessage);
					});
				}
				else
				{
					base.Dispatcher.Post(delegate
					{
						StatusBusyIndicator.Hide();
						StatusEllipse.Show();
						StatusEllipse.Fill = global::ForkPlus.UI.Theme.ApplicationColors.GreenBrush;
						StatusTextBlock.Text = Translate("Online");
						global::Avalonia.Controls.ToolTip.SetTip(StatusTextBlock,null);
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
