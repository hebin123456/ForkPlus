using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Navigation;
using ForkPlus.Accounts;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public partial class AccountDetailsUserControl : UserControl
	{
		[Null]
		private Account _account;

		public AccountDetailsUserControl()
		{
			InitializeComponent();
		}

		public void ShowDetails([Null] Account account)
		{
			_account = account;
			Refresh();
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.OriginalSource is TabControl)
			{
				Refresh();
			}
		}

		private void Refresh()
		{
			if (_account == null)
			{
				FallbackUserControl.Show();
				return;
			}
			FallbackUserControl.Hide();
			AvatarImage.Url = _account.AvatarUrl;
			HeaderUserNameTextBlock.Text = _account.Username;
			HeaderProfileUrlHyperlink.NavigateUri = new Uri(_account.ServerUrl);
			HeaderProfileUrlTextBlock.Text = _account.ServerUrl;
			if (AccountDetailsTabControl.SelectedItem is AccountTabItem)
			{
				AccountTabItem.Refresh(_account);
			}
			else if (AccountDetailsTabControl.SelectedItem is AccountRepositoriesTabItem)
			{
				AccountRepositoriesTabItem.Refresh(_account);
			}
		}

	}
}
