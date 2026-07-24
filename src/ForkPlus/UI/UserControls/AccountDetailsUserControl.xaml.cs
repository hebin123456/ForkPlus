// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Documents → using Avalonia.Controls.Documents（Hyperlink）
// - using System.Windows.Markup → 移除
// - using System.Windows.Navigation → 移除（Avalonia 无 RequestNavigateEventArgs）
// - Hyperlink.RequestNavigate + RequestNavigateEventArgs.Uri → Hyperlink.Click + Hyperlink.NavigateUri（参考 GitUserControl）
// - SelectionChangedEventArgs → Avalonia.Controls 同名类型
// - e.OriginalSource → e.Source（参考 ClosableTabItem/MultiselectionTreeView）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
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

		private void Hyperlink_Click(object sender, RoutedEventArgs e)
		{
			// 阶段 4.5：WPF Hyperlink.RequestNavigate + RequestNavigateEventArgs.Uri
			// → Avalonia Hyperlink.Click + Hyperlink.NavigateUri（参考 GitUserControl）。
			if (sender is Hyperlink hyperlink && hyperlink.NavigateUri != null)
			{
				hyperlink.NavigateUri.OpenInBrowser();
			}
			e.Handled = true;
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.Source is TabControl)
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
