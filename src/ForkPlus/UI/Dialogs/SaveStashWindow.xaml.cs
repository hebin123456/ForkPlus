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

		protected override string GetCommandPreview()
		{
			bool stageNewFiles = StageNewFilesCheckBox.IsChecked.GetValueOrDefault();
			string stashMessage = StashMessageTextBox.Text;
			System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string> { "git", "stash", "push" };
			if (!string.IsNullOrWhiteSpace(stashMessage))
			{
				parts.Add("-m");
				parts.Add("\"" + stashMessage + "\"");
			}
			if (stageNewFiles)
			{
				parts.Add("--include-untracked");
			}
			return string.Join(" ", parts);
		}

		public SaveStashWindow(GitModule gitModule)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Save stash");
			base.DialogDescription = Translate("Save your local changes to a new stash");
			base.SubmitButtonTitle = Translate("Save Stash");
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

		private void StashMessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private void CheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
