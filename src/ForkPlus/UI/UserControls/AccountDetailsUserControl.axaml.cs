using System;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup;
using ForkPlus.Accounts;
using ForkPlus.UI.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

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
			// Migration note：Avalonia 在 XamlIlPopulate 解析 TabControl 的 EndInit 即自动
			// 选中第一个 tab 并同步触发 SelectionChanged（WPF 解析期不触发）；此时声明在
			// TabControl 之后的 x:Name 字段（FallbackUserControl）尚未赋值，走 Refresh() 会在
			// FallbackUserControl.Show() 处 NRE——菜单"文件→账号..."的 posted job 未捕获该异常
			// → 整个进程崩溃。初始化未完成时直接忽略（FallbackUserControl 默认可见，初始
			// 视觉状态与 Refresh(无账号) 一致，行为不变）。
			if (FallbackUserControl == null)
			{
				return;
			}
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
			Account account = _account;
			string avatarUrl = account.AvatarUrl;
			AvatarImage.Url = null;
			Dispatcher.UIThread.Post(delegate
			{
				if (_account == account)
				{
					AvatarImage.Url = avatarUrl;
				}
			}, DispatcherPriority.Background);
			HeaderUserNameTextBlock.Text = _account.Username;
			string serverUrl = _account.ServerUrl ?? "";
			HeaderProfileUrlHyperlink.NavigateUri = Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri uri) ? uri : null;
			HeaderProfileUrlTextBlock.Text = serverUrl;
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
