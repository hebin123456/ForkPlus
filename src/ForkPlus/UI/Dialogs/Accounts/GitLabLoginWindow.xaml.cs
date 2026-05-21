using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
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

		[Null]
		public Account Account { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, "");
				if (_server)
				{
					PersonalAccessTokenHint.Disable();
					if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var _))
					{
						return false;
					}
					PersonalAccessTokenHint.Enable();
				}
				return !string.IsNullOrEmpty(TokenTextBox.Text);
			}
		}

		private string ServerUrl
		{
			get
			{
				if (!_server)
				{
					return "https://gitlab.com";
				}
				return ServerTextBox.Text.ToLower().TrimEnd(Consts.Chars.Slash);
			}
		}

		public GitLabLoginWindow(bool server = false, [Null] Account account = null)
		{
			_server = server;
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.SubmitButtonTitle = "Sign In";
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
			string serverUrl = ServerUrl;
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
			new Uri(ServerUrl + "/-/user_settings/personal_access_tokens?name=Fork&scopes=api%2Cwrite_repository").OpenInBrowser();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
