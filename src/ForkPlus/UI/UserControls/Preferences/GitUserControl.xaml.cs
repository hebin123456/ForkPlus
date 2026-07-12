using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Navigation;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class GitUserControl : UserControl
	{
		public class GitInstanceItem
		{
			public string FileName { get; }

			public string GitPath { get; }

			public GitInstanceType GitInstanceType { get; }

			public static GitInstanceItem CreateEnvironmentGitInstance()
			{
				string text = GitVersion(App.EnvironmentGitInstancePath);
				if (text != null)
				{
					return new GitInstanceItem(text + " - ENV git instance " + App.EnvironmentGitInstancePath, App.EnvironmentGitInstancePath, GitInstanceType.Environment);
				}
				return null;
			}

			public static GitInstanceItem CreateLocalGitInstance()
			{
				string text = GitVersion(App.ForkGitInstancePath);
				if (text != null)
				{
					return new GitInstanceItem(text + " - Fork git instance", App.ForkGitInstancePath, GitInstanceType.Local);
				}
				return null;
			}

			public static GitInstanceItem CreateCustomGitInstance(string normalizedPath)
			{
				if (ValidatePath(normalizedPath))
				{
					string text = GitVersion(normalizedPath);
					if (text != null)
					{
						return new GitInstanceItem(text + " - " + normalizedPath, normalizedPath, GitInstanceType.Custom);
					}
				}
				return null;
			}

			public static GitInstanceItem CreateSystemGitInstance()
			{
				string text = TryFindExistingInstance(new string[3] { "%programfiles(x86)%\\Git\\bin\\git.exe", "%programfiles%\\Git\\bin\\git.exe", "%ProgramW6432%\\Git\\bin\\git.exe" });
				if (text != null)
				{
					string text2 = GitVersion(text);
					if (text2 != null)
					{
						return new GitInstanceItem(text2 + " - " + text, text, GitInstanceType.System);
					}
				}
				return null;
			}

			public static GitInstanceItem CreateSeparator()
			{
				return new GitInstanceItem(string.Empty, string.Empty, GitInstanceType.Separator);
			}

			public static GitInstanceItem CreateAddCustomGitInstance()
			{
				return new GitInstanceItem(PreferencesLocalization.Current("Custom Git Instance..."), string.Empty, GitInstanceType.AddCustom);
			}

			private GitInstanceItem(string fileName, string path, GitInstanceType itemType)
			{
				FileName = fileName;
				GitPath = path;
				GitInstanceType = itemType;
			}

			private static string GitVersion(string path)
			{
				GitCommandResult<string> gitCommandResult = new GetGitVersionGitCommand().Execute(path);
				if (gitCommandResult.Succeeded)
				{
					return gitCommandResult.Result;
				}
				return null;
			}

			private static string TryFindExistingInstance(string[] possiblePaths)
			{
				foreach (string text in possiblePaths)
				{
					try
					{
						string text2 = Environment.ExpandEnvironmentVariables(text);
						if (File.Exists(text2))
						{
							return text2;
						}
					}
					catch (Exception ex)
					{
						Log.Error("Failed to check '" + text + "' existence", ex);
					}
				}
				return null;
			}

			private static bool ValidatePath(string gitExecutablePath)
			{
				try
				{
					if (!File.Exists(gitExecutablePath))
					{
						new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find git instance at: '{0}'", gitExecutablePath)).ShowDialog();
						return false;
					}
					if (Path.GetFileName(gitExecutablePath) != "git.exe")
					{
						new ErrorWindow(PreferencesLocalization.FormatCurrent("Invalid git binary: '{0}'", gitExecutablePath)).ShowDialog();
						return false;
					}
					string directoryName = Path.GetDirectoryName(gitExecutablePath);
					if (Directory.Exists(directoryName))
					{
						if (!File.Exists(Path.Combine(directoryName, "bash.exe")))
						{
							new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find git instance at: '{0}'. Missing bash.exe", gitExecutablePath)).ShowDialog();
							return false;
						}
						if (!File.Exists(Path.Combine(directoryName, "sh.exe")))
						{
							new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find git instance at: '{0}'. Missing sh.exe", gitExecutablePath)).ShowDialog();
							return false;
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("Path validation failed '" + gitExecutablePath + "'", ex);
				}
				return true;
			}
		}

		public enum GitInstanceType
		{
			Environment,
			Local,
			System,
			Custom,
			Separator,
			AddCustom
		}

		private static readonly string VerboseGitOutputTooltip = "GIT_TRACE=true\nEnables general trace output. Shows internal Git operations like command execution, file operations, and subprocess spawning.\n\nGIT_TRACE_CURL=true\nEnables verbose output from libcurl for HTTP/HTTPS operations. Shows request/response headers, SSL handshake details, and transfer progress when using HTTP-based remotes.\n\nGIT_SSH_COMMAND=\"ssh -vvv\"\nSets the SSH command with maximum verbosity (-vvv). Shows detailed SSH connection debugging: key exchange, authentication attempts, channel operations, and protocol negotiation when using SSH-based remotes.\n\nGIT_TRACE_PACKFILE=true\nTraces packfile operations. Shows details about how Git packs and unpacks objects during fetch/push operations.\n\nGIT_TRACE_PERFORMANCE=true\nShows performance timing data. Reports how long various Git operations take, useful for diagnosing slow operations.";

		private DelayedAction<UserIdentity> _updateAvatarAction;

		private ForkPlusDialogWindow _parentWindow;

		public GitUserControl()
		{
			InitializeComponent();
			_updateAvatarAction = new DelayedAction<UserIdentity>(UpdateAvatar, 0.3);
		}

		public void Initialize(ForkPlusDialogWindow parentWindow)
		{
			_parentWindow = parentWindow;
			RefreshGitInstanceComboBox();
			VerboseGitOutputCheckBox.IsChecked = ForkPlusSettings.Default.VerboseGitOutput;
			VerboseGitOutputCheckBox.ToolTip = new TextBlock
			{
				MaxWidth = 500.0,
				TextWrapping = TextWrapping.Wrap,
				Text = VerboseGitOutputTooltip
			};
			UserIdentity result = new GetGlobalUserIdentityGitCommand().Execute().Result;
			UserNameTextBox.Text = result.Name ?? "";
			EmailTextBox.Text = result.Email ?? "";
			_updateAvatarAction.InvokeNow(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
		}

		private void VerboseGitOutputCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			ForkPlusSettings.Default.VerboseGitOutput = VerboseGitOutputCheckBox.IsChecked.GetValueOrDefault();
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
			e.Handled = true;
		}

		private void UserNameTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			SetGlobalUserIdentity();
		}

		private void EmailTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			SetGlobalUserIdentity();
		}

		private void UserNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_updateAvatarAction.InvokeWithDelay(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
		}

		private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_updateAvatarAction.InvokeWithDelay(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
		}

		private void UpdateAvatar(UserIdentity userIdentity)
		{
			AuthorAvatarImage.ShowAvatarNoCache(userIdentity);
		}

		private async void SetGlobalUserIdentity()
		{
			try
			{
				string userName = UserNameTextBox.Text.Trim();
				string email = EmailTextBox.Text.Trim();
				GitCommandResult gitCommandResult = await Task.Run(delegate
				{
					GitCommandResult gitCommandResult2 = new SetGlobalUserIdentityGitCommand().Execute(new UserIdentity(userName, email));
					return (!gitCommandResult2.Succeeded) ? gitCommandResult2 : GitCommandResult.Success();
				});
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
				}
			}
			catch (Exception ex)
			{
				Log.Error("SetGlobalUserIdentity failed", ex);
			}
		}

		private void GitInstanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			GitInstanceItem selectedItem = ((e.RemovedItems.Count > 0) ? (e.RemovedItems[0] as GitInstanceItem) : null);
			if (!(GitInstanceComboBox.SelectedItem is GitInstanceItem gitInstanceItem))
			{
				return;
			}
			switch (gitInstanceItem.GitInstanceType)
			{
			case GitInstanceType.Local:
				ForkPlusSettings.Default.GitInstancePath = null;
				break;
			case GitInstanceType.System:
				ForkPlusSettings.Default.GitInstancePath = gitInstanceItem.GitPath;
				break;
			case GitInstanceType.Custom:
				ForkPlusSettings.Default.GitInstancePath = gitInstanceItem.GitPath;
				break;
			case GitInstanceType.AddCustom:
			{
				string initialDirectory = Environment.ExpandEnvironmentVariables("%userprofile%");
				if (OpenDialog.SelectExecutableFile(_parentWindow, PreferencesLocalization.Current("Select git instance"), initialDirectory, out var filePath))
				{
					string gitInstancePath = PathHelper.Normalize(filePath);
					ForkPlusSettings.Default.GitInstancePath = gitInstancePath;
					RefreshGitInstanceComboBox();
				}
				else
				{
					GitInstanceComboBox.SelectedItem = selectedItem;
				}
				break;
			}
			}
			Log.Info("Git Location: " + App.GitPath);
			WarnIfGitVersionUnsupported(App.GitPath);
		}

		/// <summary>
		/// 选中的 git 版本过低时弹警告（不阻止选择）。
		/// </summary>
		private static void WarnIfGitVersionUnsupported(string gitPath)
		{
			try
			{
				GitVersionCheckResult result = GitVersionChecker.Check(gitPath);
				if (result.Status == GitVersionStatus.Unsupported)
				{
					string versionText = result.Version != null ? result.Version.ToString(3) : "?";
					string minText = GitVersionChecker.MinimumRequiredVersion.ToString(2);
					new ErrorWindow(PreferencesLocalization.FormatCurrent(
						"Detected git version {0} is older than the required {1}. Some features (diff, status, empty-changes detection) may not work correctly. Please upgrade git.",
						versionText, minText)).ShowDialog();
				}
				else if (result.Status == GitVersionStatus.Outdated)
				{
					string versionText = result.Version != null ? result.Version.ToString(3) : "?";
					string recText = GitVersionChecker.RecommendedVersion.ToString(2);
					new ErrorWindow(PreferencesLocalization.FormatCurrent(
						"Detected git version {0} is below the recommended {1}. Consider upgrading for better compatibility.",
						versionText, recText)).ShowDialog();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to check git version on selection", ex);
			}
		}

		private void RefreshGitInstanceComboBox()
		{
			List<GitInstanceItem> list = new List<GitInstanceItem>(5);
			GitInstanceItem gitInstanceItem = GitInstanceItem.CreateEnvironmentGitInstance();
			if (gitInstanceItem != null)
			{
				list.Add(gitInstanceItem);
			}
			GitInstanceItem gitInstanceItem2 = GitInstanceItem.CreateLocalGitInstance();
			if (gitInstanceItem2 != null)
			{
				list.Add(gitInstanceItem2);
			}
			GitInstanceItem gitInstanceItem3 = GitInstanceItem.CreateSystemGitInstance();
			if (gitInstanceItem3 != null)
			{
				list.Add(gitInstanceItem3);
			}
			string currentGitInstancePath = ForkPlusSettings.Default.GitInstancePath;
			GitInstanceItem gitInstanceItem4 = null;
			if (currentGitInstancePath != null && !list.ContainsItem((GitInstanceItem x) => x.GitPath == currentGitInstancePath))
			{
				gitInstanceItem4 = GitInstanceItem.CreateCustomGitInstance(currentGitInstancePath);
				if (gitInstanceItem4 != null)
				{
					list.Add(gitInstanceItem4);
				}
			}
			list.Add(GitInstanceItem.CreateSeparator());
			list.Add(GitInstanceItem.CreateAddCustomGitInstance());
			GitInstanceComboBox.ItemsSource = list.ToArray();
			GitInstanceComboBox.IsEnabled = true;
			if (gitInstanceItem != null)
			{
				GitInstanceComboBox.SelectedItem = gitInstanceItem;
				GitInstanceComboBox.IsEnabled = false;
			}
			else if (currentGitInstancePath == null)
			{
				GitInstanceComboBox.SelectedItem = gitInstanceItem2;
			}
			else if (gitInstanceItem3 != null && currentGitInstancePath == gitInstanceItem3.GitPath)
			{
				GitInstanceComboBox.SelectedItem = gitInstanceItem3;
			}
			else
			{
				GitInstanceComboBox.SelectedItem = gitInstanceItem4 ?? gitInstanceItem2;
			}
		}

	}
}
