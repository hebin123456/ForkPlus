using System;
using System.ComponentModel;
using System.Threading;
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
	public partial class GitHubLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private readonly AuthenticationItem[] _authenticationItems = new AuthenticationItem[2]
		{
			new AuthenticationItem(AuthenticationType.OAuth, "Log in on GitHub.com (OAuth)"),
			new AuthenticationItem(AuthenticationType.AccessToken, "Personal Access Token")
		};

		[Null]
		private CancellationTokenSource _cancellationTokenSource;

		[Null]
		public Account Account { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, "");
				if (!(AuthenticationTypeComboBox.SelectedItem is AuthenticationItem { Type: var type }))
				{
					return false;
				}
				switch (type)
				{
				case AuthenticationType.OAuth:
					if (!string.IsNullOrEmpty(UsernameTextBox.Text))
					{
						return base.IsSubmitAllowed;
					}
					return false;
				case AuthenticationType.AccessToken:
					if (!string.IsNullOrEmpty(TokenTextBox.Text))
					{
						return base.IsSubmitAllowed;
					}
					return false;
				default:
					return false;
				}
			}
		}

		public GitHubLoginWindow([Null] Account account = null)
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.SubmitButtonTitle = "Sign In";
			Account = account;
			AuthenticationTypeComboBox.ItemsSource = _authenticationItems;
			SelectAuthenticationType(account?.AuthenticationType ?? AuthenticationType.OAuth);
			UsernameTextBox.Text = account?.Username;
			UsernameTextBox.TextChanged += delegate
			{
				UpdateSubmitButton();
			};
			TokenTextBox.TextChanged += delegate
			{
				UpdateSubmitButton();
			};
			base.Dispatcher.Async(delegate
			{
				UsernameTextBox.Focus();
			});
		}

		protected override void OnCancel()
		{
			if (_cancellationTokenSource != null)
			{
				_cancellationTokenSource.Cancel();
			}
			else
			{
				base.OnCancel();
			}
		}

		protected override void OnSubmit()
		{
			if (!(AuthenticationTypeComboBox.SelectedItem is AuthenticationItem authenticationItem))
			{
				return;
			}
			if (authenticationItem.Type == AuthenticationType.AccessToken)
			{
				AuthenticateWithAccessToken();
				return;
			}
			if (authenticationItem.Type == AuthenticationType.OAuth)
			{
				AuthenticateWithOAuth();
				return;
			}
			throw new CannotReachHereException();
		}

		private void AuthenticateWithAccessToken()
		{
			string token = TokenTextBox.Text;
			GitHubAccessTokenAuthentication authentication = new GitHubAccessTokenAuthentication("https://github.com", null, token);
			Connection connection = new Connection("https://api.github.com", authentication);
			GitHubService tempService = new GitHubService(connection);
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Log in to https://github.com...");
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
						GitHubAccessTokenAuthentication gitHubAccessTokenAuthentication = new GitHubAccessTokenAuthentication("https://github.com", result.Username, token);
						if (!gitHubAccessTokenAuthentication.Save())
						{
							EnableEditableControls();
							SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
						}
						else
						{
							Account account2 = new Account(RemoteType.Github, gitHubAccessTokenAuthentication.AuthenticationType, "https://github.com", null, result.Username, result.AvatarUrl, enableNotifications: true);
							AccountManager.Current.AddOrUpdate(account2);
							Account = account2;
							MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
							CloseWithOk();
						}
					});
				}
			}, JobFlags.Hidden);
		}

		private void AuthenticateWithOAuth()
		{
			string username = UsernameTextBox.Text;
			string oauthState = OAuthWebFlowHelper.GenerateRandomState();
			_cancellationTokenSource?.Cancel();
			_cancellationTokenSource = new CancellationTokenSource();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Waiting for reponse from https://github.com...");
			base.Footer.IsEnabled = true;
			base.Footer.SubmitButton.IsEnabled = false;
			base.Footer.CancelButton.IsEnabled = true;
			_jobQueue.Add(PreferencesLocalization.Current("Log in to GitHub"), delegate
			{
				string text = "repo user notifications workflow";
				ServiceResult<string> authorizeResult = OAuthWebFlowHelper.Authorize(new UriBuilder(GitHubConsts.AuthorizeUri)
				{
					Query = "client_id=" + GitHubConsts.ClientId + "&scope=" + text + "&state=" + oauthState + "&allow_signup=false&login=" + username
				}.Uri.ToString(), oauthState, _cancellationTokenSource.Token);
				if (!authorizeResult.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						EnableEditableControls();
						_cancellationTokenSource = null;
						if (authorizeResult.Error is ServiceError.Cancelled)
						{
							OnCancel();
						}
						else
						{
							SetStatus(ForkPlusDialogStatus.Error, authorizeResult.Error.FriendlyMessage);
						}
					});
				}
				else
				{
					string result = authorizeResult.Result;
					GitHubAuthenticationService gitHubAuthenticationService = new GitHubAuthenticationService(new Connection("https://github.com", null));
					ServiceResult<OAuthToken> getAccessTokenResult = gitHubAuthenticationService.GetAccessToken(result, oauthState);
					if (!getAccessTokenResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							EnableEditableControls();
							_cancellationTokenSource = null;
							SetStatus(ForkPlusDialogStatus.Error, getAccessTokenResult.Error.FriendlyMessage);
						});
					}
					else
					{
						Log.Info("Received OAuth session");
						string oauthToken = getAccessTokenResult.Result.Token;
						GitHubOAuthAuthentication authentication = new GitHubOAuthAuthentication("https://github.com", null, oauthToken);
						GitHubService gitHubService = new GitHubService(new Connection("https://api.github.com", authentication));
						ServiceResult<User> userResponse = gitHubService.GetUser();
						if (!userResponse.Succeeded)
						{
							base.Dispatcher.Async(delegate
							{
								EnableEditableControls();
								_cancellationTokenSource = null;
								SetStatus(ForkPlusDialogStatus.Error, userResponse.Error.FriendlyMessage);
							});
						}
						else
						{
							User user = userResponse.Result;
							base.Dispatcher.Async(delegate
							{
								EnableEditableControls();
								_cancellationTokenSource = null;
								Account account = Account;
								if (account != null)
								{
									AccountManager.Current.LogOut(account);
								}
								GitHubOAuthAuthentication gitHubOAuthAuthentication = new GitHubOAuthAuthentication("https://github.com", user.Username, oauthToken);
								if (!gitHubOAuthAuthentication.Save())
								{
									SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
								}
								else
								{
									Account account2 = new Account(RemoteType.Github, gitHubOAuthAuthentication.AuthenticationType, "https://github.com", null, user.Username, user.AvatarUrl, enableNotifications: true);
									AccountManager.Current.AddOrUpdate(account2);
									Account = account2;
									MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.Remotes);
									CloseWithOk();
								}
							});
						}
					}
				}
			}, JobFlags.Hidden);
		}

		private void AuthenticationTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshAuthenticationDetails();
			UpdateSubmitButton();
		}

		private void TokenTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void RefreshAuthenticationDetails()
		{
			if (AuthenticationTypeComboBox.SelectedItem is AuthenticationItem { Type: var type })
			{
				switch (type)
				{
				case AuthenticationType.OAuth:
					TokenContainer.Collapse();
					OAuthContainer.Show();
					break;
				case AuthenticationType.AccessToken:
					TokenContainer.Show();
					OAuthContainer.Collapse();
					break;
				}
			}
		}

		private void SelectAuthenticationType(AuthenticationType authenticationType)
		{
			switch (authenticationType)
			{
			case AuthenticationType.AccessToken:
				AuthenticationTypeComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_authenticationItems, (AuthenticationItem x) => x.Type == AuthenticationType.AccessToken);
				break;
			case AuthenticationType.OAuth:
				AuthenticationTypeComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_authenticationItems, (AuthenticationItem x) => x.Type == AuthenticationType.OAuth);
				break;
			}
		}

		private void OpenPersonalAccessTokenConfigurationUrlButton_Click(object sender, RoutedEventArgs e)
		{
			new Uri("https://github.com/settings/tokens/new?description=Fork&scopes=repo,user,notifications,workflow").OpenInBrowser();
		}

	}
}
