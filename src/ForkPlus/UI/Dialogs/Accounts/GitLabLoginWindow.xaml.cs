using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.Utils.Http;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public partial class GitLabLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private readonly bool _server;

	// 阶段 3：承接 server URL 规范化(TrimEnd) + URI 校验 + token 非空校验。SetStatus/Hint 副作用留 View。
	private readonly GitLabLoginWindowViewModel _viewModel;

		[Null]
		public Account Account { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, "");
				_viewModel.ServerText = ServerTextBox.Text;
				_viewModel.Token = TokenTextBox.Text;
				if (_server)
				{
					PersonalAccessTokenHint.Disable();
					if (_viewModel.IsUriValid)
					{
						PersonalAccessTokenHint.Enable();
					}
				}
				return _viewModel.IsSubmitAllowed;
			}
		}

		public GitLabLoginWindow(bool server = false, [Null] Account account = null)
		{
			_server = server;
			_viewModel = new GitLabLoginWindowViewModel(server);
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.SubmitButtonTitle = Translate("Sign In");
			OpenPersonalAccessTokenConfigurationUrlButton.ToolTip = Translate("Required scopes: read_user, read_api, read_repository, write_repository");
			Account = account;
			if (!server)
			{
				ServerTextBlock.Collapse();
				ServerTextBox.Collapse();
			}
			else
			{
				ServerTextBox.Text = account?.ServerUrl ?? "https://gitlab.com";
				ServerTextBlock.Show();
				ServerTextBox.Show();
			}
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
			RemoteType remoteType = ((serverUrl == "https://gitlab.com") ? RemoteType.Gitlab : RemoteType.GitlabServer);
			GitLabPrivateAccessTokenAuthentication authentication = new GitLabPrivateAccessTokenAuthentication(null, null, token);
			Connection connection = new Connection(serverUrl, authentication);
			GitLabService tempService = new GitLabService(connection, remoteType == RemoteType.GitlabServer);
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Log in to " + serverUrl + "...");
			_jobQueue.Add(Translate("Get user"), delegate
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
						GitLabPrivateAccessTokenAuthentication gitLabPrivateAccessTokenAuthentication = new GitLabPrivateAccessTokenAuthentication(serverUrl, result.Username, token);
						if (!gitLabPrivateAccessTokenAuthentication.Save())
						{
							EnableEditableControls();
							SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
						}
						else
						{
							Account account2 = new Account(remoteType, gitLabPrivateAccessTokenAuthentication.AuthenticationType, serverUrl, null, result.Username, result.AvatarUrl);
							AccountManager.Current.AddOrUpdate(account2);
							Account = account2;
							MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
							CloseWithOk();
						}
					});
				}
			}, JobFlags.Hidden);
		}

		private void OpenPersonalAccessTokenConfigurationUrlButton_Click(object sender, RoutedEventArgs e)
		{
			_viewModel.ServerText = ServerTextBox.Text;
			new Uri(_viewModel.ServerUrl + "/-/user_settings/personal_access_tokens?name=Fork&scopes=api%2Cwrite_repository").OpenInBrowser();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
