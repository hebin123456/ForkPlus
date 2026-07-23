using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using ForkPlus.Accounts;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public partial class OpenAiLoginWindow : ForkPlusDialogWindow
	{
		private readonly JobQueue _jobQueue = new JobQueue();

	// 阶段 3：承接 token 非空校验（纯判断，SetStatus 副作用留 override）。
	private readonly OpenAiLoginWindowViewModel _viewModel = new OpenAiLoginWindowViewModel();

	protected override bool IsSubmitAllowed
	{
		get
		{
			SetStatus(ForkPlusDialogStatus.None, "");
			_viewModel.Token = TokenTextBox.Text;
			return _viewModel.IsSubmitAllowed;
		}
	}

		public OpenAiLoginWindow()
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.SubmitButtonTitle = PreferencesLocalization.Current("Sign In");
			TokenTextBox.TextChanged += delegate
			{
				UpdateSubmitButton();
			};
			UpdateSubmitButton();
		}

		protected override void OnSubmit()
		{
			string text = TokenTextBox.Text;
			string serverUrl = "https://api.openai.com";
			PrivateAccessTokenAuthentication authentication = new PrivateAccessTokenAuthentication(serverUrl, "generic", text);
			Connection connection = new Connection(serverUrl, authentication);
			OpenAiService service = new OpenAiService(connection);
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Signing in...");
			_jobQueue.Add(PreferencesLocalization.Current("Signing in..."), delegate
			{
				ServiceResult<OpenAiResponse> result = service.Test();
				base.Dispatcher.Async(delegate
				{
					if (!result.Succeeded)
					{
						EnableEditableControls();
						SetStatus(ForkPlusDialogStatus.Error, result.Error.FriendlyMessage);
					}
					else if (!authentication.Save())
					{
						SetStatus(ForkPlusDialogStatus.Error, "Cannot save authentication");
					}
					else
					{
						ForkPlusSettings.Default.OpenAiLoggedIn = true;
						CloseWithOk();
					}
				});
			});
		}

		private void TokenConfigurationUrlButton_Click(object sender, RoutedEventArgs e)
		{
			new Uri("https://platform.openai.com/api-keys").OpenInBrowser();
		}

	}
}
