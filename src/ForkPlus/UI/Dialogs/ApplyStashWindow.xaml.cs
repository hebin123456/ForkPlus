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
	public partial class ApplyStashWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly StashRevision _stash;

		public ApplyStashWindow(RepositoryUserControl repositoryUserControl, StashRevision stash)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_stash = stash;
			GitPointView.Value = _stash;
			base.DialogTitle = Translate("Apply Stash");
		base.DialogDescription = Translate("Apply changes of the stash to your working directory");
		base.SubmitButtonTitle = Translate("Apply");
			DeleteStashAfterApplyCheckBox.IsChecked = ForkPlusSettings.Default.ApplyStash_DeleteAfterApply;
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			StashRevision stash = _stash;
			bool deleteAfterApply = DeleteStashAfterApplyCheckBox.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.ApplyStash_DeleteAfterApply = deleteAfterApply;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Applying stash...");
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Apply stash '{0}'"), stash.ReflogName), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new ApplyStashGitCommand().Execute(gitModule, stash.ReflogName, deleteAfterApply, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void DeleteStashAfterApplyCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (DeleteStashAfterApplyCheckBox.IsChecked.GetValueOrDefault())
			{
				DeleteStashWarningImage.Show();
			}
			else
			{
				DeleteStashWarningImage.Hide();
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
