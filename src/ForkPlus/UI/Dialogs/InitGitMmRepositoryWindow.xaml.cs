using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class InitGitMmRepositoryWindow : ForkPlusDialogWindow
	{
		protected override bool IsSubmitAllowed
		{
			get
			{
				if (string.IsNullOrWhiteSpace(ManifestUrlTextBox.Text.Trim()))
				{
					return false;
				}
				if (string.IsNullOrWhiteSpace(ParentDirectoryTextBox.Text.Trim()))
				{
					return false;
				}
				if (string.IsNullOrWhiteSpace(RepositoryNameTextBox.Text.Trim()))
				{
					return false;
				}
				return base.IsSubmitAllowed;
			}
		}

		public InitGitMmRepositoryWindow()
		{
			InitializeComponent();
			base.DialogTitle = Translate("Initialize git mm Repository");
			base.DialogDescription = Translate("Initialize a git mm workspace from a manifest repository");
			base.SubmitButtonTitle = Translate("Initialize");
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			ParentDirectoryTextBox.Text = RepositoryManager.Instance.DefaultSourceDir();
			RestoreDefaults();
			UpdateSubmitButton();
			RefreshCommandPreview();
			base.Loaded += delegate
			{
				Dispatcher.BeginInvoke(new Action(RefreshCommandPreview), System.Windows.Threading.DispatcherPriority.Loaded);
				ManifestUrlTextBox.Focus();
			};
		}

		protected override async void OnSubmit()
		{
			string url = ManifestUrlTextBox.Text.Trim();
			string parentDirectory = ParentDirectoryTextBox.Text.Trim();
			string repositoryName = RepositoryNameTextBox.Text.Trim();
			string destinationDirectory = System.IO.Path.Combine(parentDirectory, repositoryName);
			string manifest = string.IsNullOrWhiteSpace(ManifestFileTextBox.Text) ? "dependency.xml" : ManifestFileTextBox.Text.Trim();
			string branch = string.IsNullOrWhiteSpace(ManifestBranchTextBox.Text) ? "master" : ManifestBranchTextBox.Text.Trim();
			string group = string.IsNullOrWhiteSpace(ManifestGroupTextBox.Text) ? "default" : ManifestGroupTextBox.Text.Trim();
			if (!ValidateDestination(destinationDirectory))
			{
				return;
			}
			SaveDefaults(url, manifest, branch, group);
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Initializing git mm repository..."));
			GitRequestResult result = await Task.Run(delegate
			{
				return RunInit(destinationDirectory, url, manifest, branch, group);
			});
			SetStatus(ForkPlusDialogStatus.None, string.Empty);
			EnableEditableControls();
			if (!result.Success)
			{
				new MessageBoxWindow("git mm init failed", result.FullReadableOutput(), "Close", "Cancel", showCancelButton: false).ShowDialog();
				return;
			}
			Application.Current.TabManager().OpenRepository(destinationDirectory);
			Close();
		}

		private bool ValidateDestination(string destinationDirectory)
		{
			try
			{
				if (Directory.Exists(destinationDirectory))
				{
					if (ForkPlus.UI.UserControls.GitMmUserControl.IsGitMmWorkspace(destinationDirectory))
					{
						new MessageBoxWindow("Already a git mm repository", string.Format(Translate("'{0}' is already a git mm repository."), destinationDirectory), "Close", "Cancel", showCancelButton: false).ShowDialog();
						return false;
					}
					if (Directory.GetFileSystemEntries(destinationDirectory).Length > 0)
					{
						new MessageBoxWindow("Destination folder is not empty", string.Format(Translate("'{0}' already exists. Please choose another name."), destinationDirectory), "Close", "Cancel", showCancelButton: false).ShowDialog();
						return false;
					}
				}
				else
				{
					Directory.CreateDirectory(destinationDirectory);
				}
				return true;
			}
			catch (Exception ex)
			{
				new MessageBoxWindow("Failed to create destination folder", ex.Message, "Close", "Cancel", showCancelButton: false).ShowDialog();
				return false;
			}
		}

		private GitRequestResult RunInit(string destinationDirectory, string url, string manifest, string branch, string group)
		{
			GitCommand command = new GitCommand("mm");
			command.AddRange(new string[9] { "init", "-u", url, "-m", manifest, "-b", branch, "-g", group });
			return default(GitRequest)
				.CurrentDir(destinationDirectory)
				.Command(command)
				.Env(new (string, string)[1] { ("GIT_TERMINAL_PROMPT", "0") })
				.Execute(new JobMonitor());
		}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = RepositoryManager.Instance.DefaultSourceDir();
			if (OpenDialog.SelectDirectory(this, Translate("Select location"), initialDirectory, out var directoryPath))
			{
				ParentDirectoryTextBox.Text = directoryPath;
				ParentDirectoryTextBox.Focus();
			}
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void RestoreDefaults()
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			ManifestUrlTextBox.Text = settings.InitUrl ?? "";
			ManifestFileTextBox.Text = settings.InitManifest;
			ManifestBranchTextBox.Text = settings.InitBranch;
			ManifestGroupTextBox.Text = settings.InitGroup;
			RefreshCommandPreview();
		}

		private void RefreshCommandPreview()
		{
			if (CommandPreviewTextBlock == null)
			{
				return;
			}
			CommandPreviewTextBlock.Text = GitMmCommandPreviewHelper.Format(CreateInitArgs());
		}

		private string[] CreateInitArgs()
		{
			string url = ManifestUrlTextBox.Text.Trim();
			string manifest = string.IsNullOrWhiteSpace(ManifestFileTextBox.Text) ? "dependency.xml" : ManifestFileTextBox.Text.Trim();
			string branch = string.IsNullOrWhiteSpace(ManifestBranchTextBox.Text) ? "master" : ManifestBranchTextBox.Text.Trim();
			string group = string.IsNullOrWhiteSpace(ManifestGroupTextBox.Text) ? "default" : ManifestGroupTextBox.Text.Trim();
			return new string[9] { "init", "-u", url, "-m", manifest, "-b", branch, "-g", group };
		}

		private void SaveDefaults(string url, string manifest, string branch, string group)
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
				settings.Workspaces ?? new string[0],
				settings.ActiveWorkspace,
				settings.ActiveSubrepo,
				settings.ActiveSubrepos,
				settings.SubrepoOrders,
				settings.VisibleSubrepos,
				settings.CommandOutputCollapsed,
				settings.CommandOutputHeight,
				settings.CommandHistory,
				settings.UploadLinks,
				settings.UploadLinksByWorkspace,
				settings.SyncJobs,
				settings.StartBranch,
				url,
				manifest,
				branch,
				group,
				settings.DialogOptions);
			ForkPlusSettings.Default.Save();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
