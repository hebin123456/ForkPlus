using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Dialogs
{
	public partial class WelcomeWindow : ForkPlusDialogWindow
	{

		protected override bool IsSubmitAllowed
		{
			get
			{
				string text = DefaultCloneDirectoryTextBox.Text.Trim();
				if (text == "" || text.Equals("c:\\", StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				try
				{
					if (!Directory.Exists(text))
					{
						return false;
					}
				}
				catch (Exception ex)
				{
					Log.Error("Failed to check '" + text + "' existence", ex);
					return false;
				}
				return true;
			}
		}

		public WelcomeWindow()
		{
			base.ShowLogo = false;
			InitializeComponent();
			// Migration note：WPF 构造期 TitleTextBlock 已就绪可直接改属性；Avalonia 12 的 chrome
			// 延迟初始化，改走 CustomizeTitleTextBlock pending 机制（构造期安全）。
			CustomizeTitleTextBlock(delegate(TextBlock t)
			{
				t.FontSize = 18.0;
				t.Foreground = Application.Current.TryFindResource("ForegroundBrush.WindowsInfo") as Brush;
			});
			base.DialogTitle = Translate("User information");
			base.DialogDescription = Translate("Set up your user name and email address. This information will be associated with your Git commits.");
			base.SubmitButtonTitle = Translate("Finish");
			ProgressBarContainer.Collapse();
			Refresh();
			DefaultCloneDirectoryTextBox.Text = SystemEnvironment.UserProfileDirectory;
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
					// Migration note：与 App.DoShutdown 同因——启动期直接 Lifetime.Shutdown 会关闭
					// Dispatcher，MainLoop 内的 PushFrame 抛 "Dispatcher shut down"；改为 Post 延迟。
					global::Avalonia.Threading.Dispatcher.UIThread.Post(delegate
					{
						(global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
					});
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
			string initialDirectory = SystemEnvironment.UserProfileDirectory;
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
