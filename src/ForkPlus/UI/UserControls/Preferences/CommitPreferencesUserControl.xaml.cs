using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Shapes;
using ForkPlus.Accounts;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs.Accounts;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class CommitPreferencesUserControl : UserControl
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private bool _initialized;

		public CommitPreferencesUserControl()
		{
			InitializeComponent();
		}

		public void Initialize()
		{
			SystemComboBoxItem.Content = PreferencesLocalization.FormatCurrent("System ({0})", CultureInfo.InstalledUICulture.Name);
			switch (ForkPlusSettings.Default.CommitSpellCheckingMode)
			{
			case CommitSpellCheckingMode.Disable:
				CommintSpellCheckingComboBox.SelectedItem = DisableComboBoxItem;
				break;
			case CommitSpellCheckingMode.System:
				CommintSpellCheckingComboBox.SelectedItem = SystemComboBoxItem;
				break;
			case CommitSpellCheckingMode.English:
				CommintSpellCheckingComboBox.SelectedItem = EnglishComboBoxItem;
				break;
			}
			CommitSubjectLowLimitTextBox.Text = ForkPlusSettings.Default.CommitSubjectLowLimit.ToString();
			CommitSubjectHighLimitTextBox.Text = ForkPlusSettings.Default.CommitSubjectHighLimit.ToString();
			PageGuideLinePositionTextBox.Text = ForkPlusSettings.Default.PageGuideLinePosition.ToString();
			RefreshOpenAiControls();
			_initialized = true;
		}

		private void RefreshOpenAiControls()
		{
			if (!ForkPlusSettings.Default.OpenAiLoggedIn)
			{
				OpenAiLoginButton.Show();
				OpenAiStatusContainer.Collapse();
				OpenAiLogoutButton.Collapse();
				return;
			}
			OpenAiLoginButton.Collapse();
			OpenAiStatusContainer.Show();
			OpenAiStatusBusyIndicator.Show();
			OpenAiStatusEllipse.Collapse();
			OpenAiStatusTextBlock.Text = PreferencesLocalization.Current("Updating...");
			PrivateAccessTokenAuthentication authentication = new PrivateAccessTokenAuthentication("https://api.openai.com", "generic");
			Connection connection = new Connection("https://api.openai.com", authentication);
			OpenAiService service = new OpenAiService(connection);
			_jobQueue.Add(PreferencesLocalization.Current("Signing in..."), delegate
			{
				ServiceResult<OpenAiResponse> result = service.Test();
				base.Dispatcher.Async(delegate
				{
					OpenAiStatusBusyIndicator.Collapse();
					OpenAiStatusEllipse.Show();
					if (!result.Succeeded)
					{
						OpenAiStatusEllipse.Fill = Theme.ApplicationColors.YellowBrush;
						int num = result.Error.FriendlyMessage.IndexOf(':');
						string text = ((num > 0) ? result.Error.FriendlyMessage.Substring(0, num) : result.Error.FriendlyMessage);
						OpenAiStatusTextBlock.Text = text;
						OpenAiStatusTextBlock.ToolTip = result.Error.FriendlyMessage;
						OpenAiLogoutButton.Show();
					}
					else
					{
						OpenAiStatusEllipse.Fill = Theme.ApplicationColors.GreenBrush;
						OpenAiStatusTextBlock.Text = PreferencesLocalization.Current("Logged in");
						OpenAiStatusTextBlock.ToolTip = null;
						OpenAiLogoutButton.Show();
					}
				});
			});
		}

		private void PageGuideLinePositionTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_initialized)
			{
				if (!int.TryParse(PageGuideLinePositionTextBox.Text, out var result))
				{
					result = 72;
				}
				ForkPlusSettings.Default.PageGuideLinePosition = result;
				NotificationCenter.Current.RaisePageGuideLinePositionChanged(this, result);
			}
		}

		private void CommintSpellCheckingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_initialized)
			{
				ComboBoxItem comboBoxItem = CommintSpellCheckingComboBox.SelectedItem as ComboBoxItem;
				if (comboBoxItem == DisableComboBoxItem)
				{
					ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.Disable;
					NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged(this, CommitSpellCheckingMode.Disable);
				}
				else if (comboBoxItem == SystemComboBoxItem)
				{
					ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.System;
					NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged(this, CommitSpellCheckingMode.System);
				}
				else if (comboBoxItem == EnglishComboBoxItem)
				{
					ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.English;
					NotificationCenter.Current.RaiseCommitSpellCheckingModeChanged(this, CommitSpellCheckingMode.English);
				}
			}
		}

		private void CommitSubjectLowLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!int.TryParse(CommitSubjectLowLimitTextBox.Text, out var result))
			{
				result = 50;
			}
			ForkPlusSettings.Default.CommitSubjectLowLimit = result;
			NotificationCenter.Current.RaiseCommitSubjectLowLimitChanged(this, result);
		}

		private void CommitSubjectHighLimitTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!int.TryParse(CommitSubjectHighLimitTextBox.Text, out var result))
			{
				result = 70;
			}
			ForkPlusSettings.Default.CommitSubjectHighLimit = result;
			NotificationCenter.Current.RaiseCommitSubjectHighLimitChanged(this, result);
		}

		private void OpenAiLoginButton_Click(object sender, RoutedEventArgs e)
		{
			if (new OpenAiLoginWindow().ShowDialog().GetValueOrDefault() && ForkPlusSettings.Default.OpenAiLoggedIn)
			{
				OpenAiLoginButton.Collapse();
				OpenAiStatusContainer.Show();
				OpenAiLogoutButton.Show();
				OpenAiStatusEllipse.Fill = Theme.ApplicationColors.GreenBrush;
				OpenAiStatusTextBlock.Text = PreferencesLocalization.Current("Logged in");
			}
		}

		private void OpenAiLogoutButton_Click(object sender, RoutedEventArgs e)
		{
			new PrivateAccessTokenAuthentication("https://api.openai.com", "generic").Destroy();
			ForkPlusSettings.Default.OpenAiLoggedIn = false;
			OpenAiLogoutButton.Collapse();
			OpenAiStatusContainer.Collapse();
			OpenAiLoginButton.Show();
		}

	}
}
