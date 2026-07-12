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
	public partial class GitHubEnterpriseLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		[Null]
		public Account Account { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, "");
				PersonalAccessTokenHint.Disable();
				if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var _))
				{
					return false;
				}
				PersonalAccessTokenHint.Enable();
				return !string.IsNullOrEmpty(TokenTextBox.Text);
			}
		}

		private string ServerUrl => ServerTextBox.Text.ToLower().TrimEnd(Consts.Chars.Slash);

		public GitHubEnterpriseLoginWindow([Null] Account account = null)
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
			string serverUrl = ServerUrl;
			string token = TokenTextBox.Text;
			Uri uri = new Uri(serverUrl);
			PrivateAccessTokenAuthentication authentication = new PrivateAccessTokenAuthentication(null, null, token);
			Connection connection = new Connection(serverUrl, authentication);
			GitHubService tempService = new GitHubService(connection, gitHubEnterprise: true);
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
								Account account2 = new Account(RemoteType.GithubEnterprise, privateAccessTokenAuthentication.AuthenticationType, serverUrl, null, result.Username, result.AvatarUrl);
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

		private void OpenPersonalAccessTokenConfigurationUrlButton_Click(object sender, RoutedEventArgs e)
		{
			new Uri(ServerUrl + "/settings/tokens/new?description=Fork&scopes=repo,user,notifications,workflow").OpenInBrowser();
		}

	}
}
