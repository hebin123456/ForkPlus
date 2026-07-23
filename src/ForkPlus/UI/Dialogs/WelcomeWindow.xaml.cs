using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class WelcomeWindow : ForkPlusDialogWindow
	{
		// 阶段 3：承接默认克隆目录校验（非空 + 非 c:\ + 目录存在）。
		private readonly WelcomeWindowViewModel _viewModel = new WelcomeWindowViewModel();

		protected override bool IsSubmitAllowed
		{
			get
			{
				_viewModel.DefaultCloneDirectory = DefaultCloneDirectoryTextBox.Text;
				return _viewModel.IsSubmitAllowed;
			}
		}

		public WelcomeWindow()
		{
			base.ShowLogo = false;
			InitializeComponent();
			base.TitleTextBlock.FontSize = 18.0;
			base.TitleTextBlock.Foreground = Application.Current.TryFindResource("ForegroundBrush.WindowsInfo") as Brush;
			base.DialogTitle = Translate("User information");
			base.DialogDescription = Translate("Set up your user name and email address. This information will be associated with your Git commits.");
			base.SubmitButtonTitle = Translate("Finish");
			ProgressBarContainer.Collapse();
			Refresh();
			DefaultCloneDirectoryTextBox.Text = Environment.ExpandEnvironmentVariables("%userprofile%");
		}

		protected override async void OnSubmit()
		{
			try
			{
				string username = UserNameTextBox.Text.Trim();
				string email = EmailNameTextBox.Text.Trim();
				string text = DefaultCloneDirectoryTextBox.Text.Trim();
				RepositoryManager.Instance.SetSourceDirs(new string[1] { text });
				DisableEditableControls();
				GitCommandResult gitCommandResult = await Task.Run(delegate
				{
					if (username != "" && email != "")
					{
						GitCommandResult gitCommandResult2 = new SetGlobalUserIdentityGitCommand().Execute(new UserIdentity(username, email));
						if (!gitCommandResult2.Succeeded)
						{
							return gitCommandResult2;
						}
					}
					new RescanUserRepositoriesCommand().Execute(reset: true);
					return GitCommandResult.Success();
				});
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
					Application.Current.Shutdown();
					return;
				}
				ForkPlusSettings.Default.Guid = Guid.NewGuid().ToString();
				ForkPlusSettings.Default.Save();
				EnableEditableControls();
				CloseWithOk();
			}
			catch (Exception ex)
			{
				Log.Error("OnSubmit failed", ex);
			}
		}

		private void UserNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void EmailNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void DefaultCloneDirectoryTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = Environment.ExpandEnvironmentVariables("%userprofile%");
			if (OpenDialog.SelectDirectory(this, Translate("Select location"), initialDirectory, out var directoryPath))
			{
				DefaultCloneDirectoryTextBox.Text = directoryPath;
				DefaultCloneDirectoryTextBox.Focus();
			}
		}

		private void Refresh()
		{
			UserIdentity result = new GetGlobalUserIdentityGitCommand().Execute().Result;
			UserNameTextBox.Text = result.Name ?? "";
			EmailNameTextBox.Text = result.Email ?? "";
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
