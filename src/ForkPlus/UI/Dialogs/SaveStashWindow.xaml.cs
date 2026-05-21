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
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class SaveStashWindow : ForkPlusDialogWindow
	{
		private GitModule _gitModule;

		public SaveStashWindow(GitModule gitModule)
		{
			InitializeComponent();
			base.DialogTitle = "Save stash";
			base.DialogDescription = "Save your local changes to a new stash";
			base.SubmitButtonTitle = "Save Stash";
			_gitModule = gitModule;
			StashMessageTextBox.Placeholder = Translate("Stash message (optional)");
			StageNewFilesCheckBox.IsChecked = ForkPlusSettings.Default.SaveStash_StageNewFiles;
		}

		protected override void OnSubmit()
		{
			bool stageNewFiles = StageNewFilesCheckBox.IsChecked.GetValueOrDefault();
			string stashMessage = (string.IsNullOrWhiteSpace(StashMessageTextBox.Text) ? null : StashMessageTextBox.Text);
			ForkPlusSettings.Default.SaveStash_StageNewFiles = stageNewFiles;
			ForkPlusSettings.Default.Save();
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Stashing...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("Stash local changes"), delegate(JobMonitor monitor)
			{
				GitCommandResult<bool> result = new SaveStashGitCommand().Execute(_gitModule, stashMessage, stageNewFiles, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(GitCommandResult.Failure(result.Error));
				});
			}, JobFlags.SaveToLog);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
