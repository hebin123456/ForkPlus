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
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
	{
		if (_stash == null || string.IsNullOrEmpty(_stash.ReflogName))
		{
			return null;
		}
		bool deleteAfterApply = DeleteStashAfterApplyCheckBox.IsChecked.GetValueOrDefault();
		return "git stash " + (deleteAfterApply ? "pop" : "apply") + " " + _stash.ReflogName;
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
		RefreshCommandPreview();
	}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
