using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public partial class GiteaLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
	{
		private readonly JobQueue _jobQueue = new JobQueue();

	// 阶段 3：承接 server URL 规范化(TrimEnd) + URI 校验 + token 非空校验。
	private readonly GiteaLoginWindowViewModel _viewModel = new GiteaLoginWindowViewModel();

	[Null]
	public Account Account { get; private set; }

	protected override bool IsSubmitAllowed
	{
		get
		{
			SetStatus(ForkPlusDialogStatus.None, "");
			PersonalAccessTokenHint.Disable();
			_viewModel.ServerText = ServerTextBox.Text;
			_viewModel.Token = TokenTextBox.Text;
			if (_viewModel.IsUriValid)
			{
				PersonalAccessTokenHint.Enable();
			}
			return _viewModel.IsSubmitAllowed;
		}
	}

		public GiteaLoginWindow([Null] Account account = null)
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.SubmitButtonTitle = PreferencesLocalization.Current("Sign In");
			Account = account;
			ServerTextBox.Text = account?.ServerUrl;
			ServerTextBox.TextChanged += delegate
			{
				UpdateSubmitButton();
			};
			TokenTextBox.TextChanged += delegate
			{
				UpdateSubmitButton();
			};
			UpdateSubmitButton();
		}

		protected override void OnSubmit()
		{
			_viewModel.ServerText = ServerTextBox.Text;
			string serverUrl = _viewModel.ServerUrl;
			string token = TokenTextBox.Text;
			Uri uri = new Uri(serverUrl);
			PrivateAccessTokenAuthentication authentication = new PrivateAccessTokenAuthentication(null, null, token);
			Connection connection = new Connection(serverUrl, authentication);
			GiteaService tempService = new GiteaService(connection);
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Log in to " + serverUrl + "...");
			_jobQueue.Add(PreferencesLocalization.Current("Get user"), delegate
			{
				ServiceResult<User> userResponse = tempService.GetUser();
				if (!userResponse.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						EnableEditableControls();
						SetStatus(ForkPlusDialogStatus.Error, userResponse.Error.FriendlyMessage);
					});
				}
				else
				{
					base.Dispatcher.Async(delegate
					{
						Account account = Account;
						if (account != null)
						{
							AccountManager.Current.LogOut(account);
						}
						User result = userResponse.Result;
						if (AccountManager.Current.FindAccount(uri.Host, result.Username) != null)
						{
							EnableEditableControls();
							SetStatus(ForkPlusDialogStatus.Warning, "You are already logged in to " + serverUrl + " as " + result.Username);
						}
						else
						{
							PrivateAccessTokenAuthentication privateAccessTokenAuthentication = new PrivateAccessTokenAuthentication(serverUrl, result.Username, token);
							if (!privateAccessTokenAuthentication.Save())
							{
								EnableEditableControls();
								SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
							}
							else
							{
								Account account2 = new Account(RemoteType.Gitea, privateAccessTokenAuthentication.AuthenticationType, serverUrl, null, result.Username, result.AvatarUrl, enableNotifications: true);
								AccountManager.Current.AddOrUpdate(account2);
								Account = account2;
								MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
								CloseWithOk();
							}
						}
					});
				}
			}, JobFlags.Hidden);
		}

		private void OpenAccessTokenConfigurationUrlButton_Click(object sender, RoutedEventArgs e)
		{
			_viewModel.ServerText = ServerTextBox.Text;
			new Uri(_viewModel.ServerUrl + "/user/settings/applications").OpenInBrowser();
		}

	}
}
