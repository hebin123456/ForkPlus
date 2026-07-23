using Avalonia.Controls.Selection;
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
	public partial class BitbucketLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private readonly AuthenticationItem[] _authenticationItems = new AuthenticationItem[1]
		{
			new AuthenticationItem(AuthenticationType.AccessToken, "API Token")
		};

	// 阶段 3：承接认证类型选择 + email + token 非空校验。SetStatus(None,"") 副作用留 View override。
	private readonly BitbucketLoginWindowViewModel _viewModel = new BitbucketLoginWindowViewModel();

		[Null]
		public Account Account { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, "");
				_viewModel.SelectedAuthenticationType = (AuthenticationTypeComboBox.SelectedItem as AuthenticationItem)?.Type;
				_viewModel.Email = EmailTextBox.Text;
				_viewModel.Token = TokenTextBox.Text;
				return _viewModel.IsSubmitAllowed;
			}
		}

		public BitbucketLoginWindow([Null] Account account = null)
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.SubmitButtonTitle = Translate("Sign In");
			Account = account;
			AuthenticationTypeComboBox.ItemsSource = _authenticationItems;
			SelectAuthenticationType(AuthenticationType.AccessToken);
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
