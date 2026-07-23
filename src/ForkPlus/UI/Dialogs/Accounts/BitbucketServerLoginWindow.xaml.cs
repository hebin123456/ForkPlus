using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public partial class BitbucketServerLoginWindow : ForkPlusDialogWindow, IServiceLoginWindow
	{
		private readonly JobQueue _jobQueue = new JobQueue();

	// 阶段 3：承接 server URL 规范化 + URI 校验 + token 非空校验。
	// SetStatus/Hint.Enable/Disable 副作用留 override，纯判断进 VM。ServerUrl 规范化移入 VM。
	private readonly BitbucketServerLoginWindowViewModel _viewModel = new BitbucketServerLoginWindowViewModel();

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

		public BitbucketServerLoginWindow([Null] Account account = null)
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
			PrivateAccessTokenAuthentication authentication = new PrivateAccessTokenAuthentication(null, null, token);
			Connection connection = new Connection(serverUrl, authentication);
			BitbucketServerService tempService = new BitbucketServerService(connection);
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
						PrivateAccessTokenAuthentication privateAccessTokenAuthentication = new PrivateAccessTokenAuthentication(serverUrl, result.Username, token);
						if (!privateAccessTokenAuthentication.Save())
						{
							EnableEditableControls();
							SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
						}
						else
						{
							Account account2 = new Account(RemoteType.BitbucketServer, privateAccessTokenAuthentication.AuthenticationType, serverUrl, null, result.Username, result.AvatarUrl);
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
			new Uri(_viewModel.ServerUrl + "/plugins/servlet/access-tokens/add").OpenInBrowser();
		}

	}
}
