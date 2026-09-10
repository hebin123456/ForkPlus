using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using ForkPlus.Settings;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public partial class AddAccountWindow : ForkPlusDialogWindow
	{
		public class ServiceViewModel : INotifyPropertyChanged
		{
			public RemoteType ServiceType { get; }

			public string ServiceName => ServiceType.FriendlyName();

			public global::Avalonia.Media.IImage Icon => ServiceType.Icon();

			public event PropertyChangedEventHandler PropertyChanged;

			public ServiceViewModel(RemoteType serviceType)
			{
				ServiceType = serviceType;
			}
		}

		private static readonly ServiceViewModel[] _servicesViewModels = new ServiceViewModel[7]
		{
			new ServiceViewModel(RemoteType.Bitbucket),
			new ServiceViewModel(RemoteType.BitbucketServer),
			new ServiceViewModel(RemoteType.Gitea),
			new ServiceViewModel(RemoteType.Github),
			new ServiceViewModel(RemoteType.GithubEnterprise),
			new ServiceViewModel(RemoteType.Gitlab),
			new ServiceViewModel(RemoteType.GitlabServer)
		};

		private ServiceViewModel SelectedService => ServicesListBox.SelectedItem as ServiceViewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, "");
				if (SelectedService == null)
				{
					return false;
				}
				return true;
			}
		}

		public AddAccountWindow()
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.SubmitButtonTitle = PreferencesLocalization.Current("Log in");
			ServicesListBox.ItemsSource = _servicesViewModels;
		}

		protected override void OnSubmit()
		{
			ForkPlusDialogWindow loginWindow = SelectedService.ServiceType.GetLoginWindow();
			if (loginWindow != null && ShowLoginDialogOwnedToThis(loginWindow).GetValueOrDefault())
			{
				CloseWithOk();
			}
		}

		private void ServicesListBox_MouseDoubleClick(object sender, global::Avalonia.Input.TappedEventArgs e)
		{
			ForkPlusDialogWindow loginWindow = SelectedService.ServiceType.GetLoginWindow();
			if (loginWindow != null && ShowLoginDialogOwnedToThis(loginWindow).GetValueOrDefault())
			{
				CloseWithOk();
			}
		}

		// 修复（2026-09-10，"账号弹窗点一下被置底、可无限开新账号弹窗"）：
		// loginWindow 经 ForkPlusDialogWindow 基类构造把 owner 设成 MainWindow，但本窗口本身已是模态
		// （AddAccountWindow 经 AccountsWindow.ShowDialog 打开，AccountsWindow 又经 MainWindow.ShowDialog
		// 打开），MainWindow 已被 AccountsWindow 禁用。loginWindow 以 MainWindow 为 owner 调 ShowDialog
		// 无法正确挂到当前活跃模态链（AddAccountWindow）上——弹窗被置底、AccountsWindow 仍可交互、
		// 能再开新账号弹窗。改为把 loginWindow 的 owner 设成本窗口（AddAccountWindow），让它正确嵌套在
		// 本窗口之下、禁用本窗口，输入路由到 loginWindow（与 IR 确认框、PushWindow 编辑远端同款修复）。
		private bool? ShowLoginDialogOwnedToThis(ForkPlusDialogWindow loginWindow)
		{
			loginWindow.SetOwnerCompat(this);
			return loginWindow.ShowDialog();
		}

		private void ServicesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

	}
}
