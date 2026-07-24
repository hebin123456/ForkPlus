using Avalonia.Controls.Selection;
using System;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls;
using Avalonia.Media;
using System.ComponentModel;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public partial class AddAccountWindow : ForkPlusDialogWindow
	{
		public class ServiceViewModel : INotifyPropertyChanged
		{
			public RemoteType ServiceType { get; }

			public string ServiceName => ServiceType.FriendlyName();

			public IImage Icon => ServiceType.Icon();

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

	// 阶段 3：承接"已选服务"校验（纯判断，SetStatus 副作用留 override）。
	// ServiceViewModel（含 IImage Icon）留在 View，VM 只跟踪 RemoteType 枚举。
	private readonly AddAccountWindowViewModel _viewModel = new AddAccountWindowViewModel();

	protected override bool IsSubmitAllowed
	{
		get
		{
			SetStatus(ForkPlusDialogStatus.None, "");
			_viewModel.SelectedServiceType = SelectedService?.ServiceType;
			return _viewModel.IsSubmitAllowed;
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
			if (loginWindow != null && loginWindow.ShowDialog().GetValueOrDefault())
			{
				CloseWithOk();
			}
		}

		private void ServicesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			ForkPlusDialogWindow loginWindow = SelectedService.ServiceType.GetLoginWindow();
			if (loginWindow != null && loginWindow.ShowDialog().GetValueOrDefault())
			{
				CloseWithOk();
			}
		}

		private void ServicesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

	}
}
