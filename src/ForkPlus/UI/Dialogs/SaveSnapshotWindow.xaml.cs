using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class SaveSnapshotWindow : ForkPlusDialogWindow
	{
		private RepositoryUserControl _repositoryUserControl;

		public SaveSnapshotWindow(RepositoryUserControl repositoryUserControl)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Save snapshot");
			base.DialogDescription = Translate("Save your local changes to a new stash, but keep them in the working directory");
			base.SubmitButtonTitle = Translate("Save Snapshot");
			_repositoryUserControl = repositoryUserControl;
			StashMessageTextBox.Placeholder = Translate("Stash message (optional)");
			StageNewFilesCheckBox.IsChecked = ForkPlusSettings.Default.SaveStash_StageNewFiles;
			// InitializeComponent 期间 AddCommandPreview 已执行，但此时控件尚未赋值，
			// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
			RefreshCommandPreview();
		}

		protected override string GetCommandPreview()
		{
			// 与 SaveWorkingDirectoryAsStashGitCommand 对应：git stash push [--include-untracked] [-m "<msg>"]
			var parts = new System.Collections.Generic.List<string> { "git", "stash", "push" };
			if (StageNewFilesCheckBox.IsChecked.GetValueOrDefault())
			{
				parts.Add("--include-untracked");
			}
			string message = StashMessageTextBox.Text;
			if (!string.IsNullOrWhiteSpace(message))
			{
				parts.Add("-m");
				parts.Add(message.Contains(" ") ? "\"" + message + "\"" : message);
			}
			return string.Join(" ", parts);
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryReferences repositoryReferences = _repositoryUserControl.RepositoryData?.References;
			if (repositoryReferences == null)
			{
				return;
			}
			bool stageNewFiles = StageNewFilesCheckBox.IsChecked.GetValueOrDefault();
			string stashMessage = (string.IsNullOrWhiteSpace(StashMessageTextBox.Text) ? null : StashMessageTextBox.Text);
			string sourceString = repositoryReferences.ActiveBranch?.Name ?? repositoryReferences.HeadSha?.ToAbbreviatedString() ?? "";
			ForkPlusSettings.Default.SaveStash_StageNewFiles = stageNewFiles;
			ForkPlusSettings.Default.Save();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Stashing...");
			_repositoryUserControl.JobQueue.Add(Translate("Stash snapshot"), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new SaveWorkingDirectoryAsStashGitCommand().Execute(gitModule, stashMessage, stageNewFiles, sourceString, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private void StashMessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private void StageNewFilesCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshCommandPreview();
		}

	}
}
