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
using ForkPlus.Utils.Http;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public partial class BitbucketLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
	{
		[Null]
		private CancellationTokenSource _cancellationTokenSource;

		private readonly JobQueue _jobQueue = new JobQueue();

		private readonly AuthenticationItem[] _authenticationItems = new AuthenticationItem[2]
		{
			new AuthenticationItem(AuthenticationType.OAuth, "Log in on Bitbucket.org (OAuth)"),
			new AuthenticationItem(AuthenticationType.AccessToken, "API Token")
		};

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
					return base.IsSubmitAllowed;
				case AuthenticationType.AccessToken:
					if (!string.IsNullOrEmpty(EmailTextBox.Text))
					{
						return !string.IsNullOrEmpty(TokenTextBox.Text);
					}
					return false;
				default:
					return false;
				}
			}
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

		public BitbucketLoginWindow([Null] Account account = null)
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.SubmitButtonTitle = "Sign In";
			Account = account;
			AuthenticationTypeComboBox.ItemsSource = _authenticationItems;
			SelectAuthenticationType(account?.AuthenticationType ?? AuthenticationType.OAuth);
			EmailTextBox.Text = account?.Email;
			OpenApiTokensConfigurationUrlButton.ToolTip = Translate("Required scopes:\n read:user:bitbucket\n read:repository:bitbucket\n read:workspace:bitbucket\n write:repository:bitbucket\n read:pullrequest:bitbucket");
			EmailTextBox.TextChanged += delegate
			{
				UpdateSubmitButton();
			};
			TokenTextBox.TextChanged += delegate
			{
				UpdateSubmitButton();
			};
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

		private void AuthenticationTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshAuthenticationDetails();
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

		private void AuthenticateWithAccessToken()
		{
			string email = EmailTextBox.Text.ToLower();
			string token = TokenTextBox.Text;
			BasicAuthentication authentication = new BasicAuthentication(null, email, token);
			Connection connection = new Connection("https://api.bitbucket.org", authentication);
			BitbucketService tempService = new BitbucketService(connection);
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Log in to https://bitbucket.org...");
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
						BitbucketBasicAuthentication bitbucketBasicAuthentication = new BitbucketBasicAuthentication("https://bitbucket.org", email, result.Username, token);
						if (!bitbucketBasicAuthentication.Save())
						{
							EnableEditableControls();
							SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
						}
						else
						{
							Account account2 = new Account(RemoteType.Bitbucket, bitbucketBasicAuthentication.AuthenticationType, "https://bitbucket.org", email, result.Username, result.AvatarUrl);
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
			_cancellationTokenSource?.Cancel();
			_cancellationTokenSource = new CancellationTokenSource();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Waiting for response from https://bitbucket.org...");
			base.Footer.IsEnabled = true;
			base.Footer.SubmitButton.IsEnabled = false;
			base.Footer.CancelButton.IsEnabled = true;
			_jobQueue.Add(Translate("Log in to Bitbucket"), delegate
			{
				ServiceResult<string> authorizeResult = OAuthWebFlowHelper.Authorize(new UriBuilder(BitbucketConsts.AuthorizeUri)
				{
					Query = "client_id=" + BitbucketConsts.ClientId + "&redirect_uri=" + BitbucketConsts.CallbackUri + "&response_type=code"
				}.Uri.ToString(), null, _cancellationTokenSource.Token);
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
					BasicAuthentication authentication = new BasicAuthentication(null, BitbucketConsts.ClientId, BitbucketConsts.ClientSecret);
					BitbucketAuthenticationService bitbucketAuthenticationService = new BitbucketAuthenticationService(new Connection("https://bitbucket.org", authentication));
					ServiceResult<OAuthToken> getAccessTokenResult = bitbucketAuthenticationService.GetAccessToken(result);
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
						OAuthToken oauthToken = getAccessTokenResult.Result;
						PrivateAccessTokenAuthentication authentication2 = new PrivateAccessTokenAuthentication(null, null, oauthToken.Token);
						BitbucketService bitbucketService = new BitbucketService(new Connection("https://api.bitbucket.org", authentication2));
						ServiceResult<User> userResponse = bitbucketService.GetUser();
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
								BitbucketOAuthAuthentication bitbucketOAuthAuthentication = new BitbucketOAuthAuthentication("https://bitbucket.org", user.Username, oauthToken);
								if (!bitbucketOAuthAuthentication.Save())
								{
									SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
								}
								else
								{
									Account account2 = new Account(RemoteType.Bitbucket, bitbucketOAuthAuthentication.AuthenticationType, "https://bitbucket.org", null, user.Username, user.AvatarUrl);
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

		private void OpenApiTokensConfigurationUrlButton_Click(object sender, RoutedEventArgs e)
		{
			new Uri("https://id.atlassian.com/manage-profile/security/api-tokens").OpenInBrowser();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
