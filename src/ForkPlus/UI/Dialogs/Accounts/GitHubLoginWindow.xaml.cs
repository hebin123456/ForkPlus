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
	public partial class GitHubLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private readonly AuthenticationItem[] _authenticationItems = new AuthenticationItem[1]
		{
			new AuthenticationItem(AuthenticationType.AccessToken, "Personal Access Token")
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
			SelectAuthenticationType(AuthenticationType.AccessToken);
			TokenTextBox.TextChanged += delegate
			{
				UpdateSubmitButton();
			};
			base.Dispatcher.Async(delegate
			{
				TokenTextBox.Focus();
			});
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

		private void AuthenticationTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshAuthenticationDetails();
			UpdateSubmitButton();
		}

		private void TokenTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void RefreshAuthenticationDetails()
		{
			if (AuthenticationTypeComboBox.SelectedItem is AuthenticationItem { Type: var type })
			{
				switch (type)
				{
				case AuthenticationType.AccessToken:
					TokenContainer.Show();
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
			}
		}

		private void OpenPersonalAccessTokenConfigurationUrlButton_Click(object sender, RoutedEventArgs e)
		{
			new Uri("https://github.com/settings/tokens/new?description=Fork&scopes=repo,user,notifications,workflow").OpenInBrowser();
		}

	}
}
