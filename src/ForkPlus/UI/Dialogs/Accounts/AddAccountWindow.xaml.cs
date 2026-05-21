using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public partial class AddAccountWindow : ForkPlusDialogWindow
	{
		public class ServiceViewModel : INotifyPropertyChanged
		{
			public RemoteType ServiceType { get; }

			public string ServiceName => ServiceType.FriendlyName();

			public ImageSource Icon => ServiceType.Icon();

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
			base.SubmitButtonTitle = "Log in";
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
