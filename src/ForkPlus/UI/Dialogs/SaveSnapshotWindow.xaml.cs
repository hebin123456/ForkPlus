using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
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
			base.DialogTitle = "Save snapshot";
			base.DialogDescription = "Save your local changes to a new stash, but keep them in the working directory";
			base.SubmitButtonTitle = "Save Snapshot";
			_repositoryUserControl = repositoryUserControl;
			StashMessageTextBox.Placeholder = Translate("Stash message (optional)");
			StageNewFilesCheckBox.IsChecked = ForkPlusSettings.Default.SaveStash_StageNewFiles;
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

	}
}
