using Avalonia.Controls.Selection;
using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public partial class AccountsWindow : ForkPlusDialogWindow
	{
		private ObservableCollection<AccountViewModel> _accountViewModels;

		public AccountsWindow()
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			Refresh();
			AccountsListBox.SelectedItem = _accountViewModels.FirstOrDefault();
			base.CancelButtonTitle = Translate("Close");
			base.ShowSubmitButton = false;
			AccountDetailsUserControl.AccountTabItem.UpdateTokenButtonClicked += AccountDetailsUserControl_UpdateTokenButtonClicked;
		}

		private void AccountsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			Account account = (AccountsListBox.SelectedItem as AccountViewModel)?.Account;
			AccountDetailsUserControl.ShowDetails(account);
		}

		private void AddAccountButton_Click(object sender, RoutedEventArgs e)
		{
			if (new AddAccountWindow
			{
				Owner = this
			}.ShowDialog().GetValueOrDefault())
			{
				Activate();
			}
			Refresh();
			AccountsListBox.SelectedItem = _accountViewModels.LastOrDefault();
			MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
		}

		private void LogOutButton_Click(object sender, RoutedEventArgs e)
		{
			Account account = (AccountsListBox.SelectedItem as AccountViewModel)?.Account;
			if (account != null && new MessageBoxWindow(string.Format(Translate("Log out of {0}?"), account.ServerUrl), Translate("You can always log back in at any time"), Translate("Log out"), Translate("Cancel"), showCancelButton: true, 500.0)
			{
				Owner = this
			}.ShowDialog().GetValueOrDefault())
			{
				AccountManager.Current.LogOut(account);
				Refresh();
				AccountsListBox.SelectedItem = _accountViewModels.FirstOrDefault();
				MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
			}
		}

		private void AccountDetailsUserControl_UpdateTokenButtonClicked(object sender, EventArgs<Account> e)
		{
			Account value = e.Value;
			if (value == null || !(AccountsListBox.SelectedItem is AccountViewModel { Account: { } account } accountViewModel) || value != account)
			{
				return;
			}
			ForkPlusDialogWindow loginWindow = value.ServiceType.GetLoginWindow(value);
			if (loginWindow != null && loginWindow.ShowDialog().GetValueOrDefault())
			{
				Account account2 = (loginWindow as IServiceLoginWindow)?.Account;
				if (account2 != null)
				{
					_accountViewModels.Remove(accountViewModel);
					AccountViewModel accountViewModel2 = new AccountViewModel(account2);
					_accountViewModels.Add(accountViewModel2);
					AccountsListBox.SelectedItem = accountViewModel2;
				}
			}
		}

		private void Refresh()
		{
			Account[] accounts = AccountManager.Current.Accounts;
			_accountViewModels = new ObservableCollection<AccountViewModel>(accounts.Map((Account x) => new AccountViewModel(x)));
			AccountsListBox.ItemsSource = _accountViewModels;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
