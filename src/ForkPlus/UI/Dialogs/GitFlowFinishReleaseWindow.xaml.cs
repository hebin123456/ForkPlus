using Avalonia.Controls.Selection;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitFlowFinishReleaseWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly LocalBranch _releaseBranch;

		private readonly RepositoryData _repositoryData;

		private GitFlowSettings _gitFlowSettings;

		private IReadOnlyList<LocalBranch> _allReleaseBranches;

		public GitFlowFinishReleaseWindow(GitModule gitModule, RepositoryData repositoryData, LocalBranch releaseBranch)
		{
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Finish Git Flow release");
			base.DialogDescription = PreferencesLocalization.Current("Finish the release and merge it into the develop and master branches");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Finish");
			_gitModule = gitModule;
			_repositoryData = repositoryData;
			_releaseBranch = releaseBranch;
			Refresh();
		}

		protected override string GetCommandPreview()
	{
		if (!(BranchesComboBox.SelectedItem is LocalBranch localBranch) || _gitFlowSettings == null)
		{
			return null;
		}
		string release = localBranch.Name.Remove(0, _gitFlowSettings.ReleasePrefix.Length);
		return "git flow release finish " + release;
	}

	protected override void OnSubmit()
	{
		if (!(BranchesComboBox.SelectedItem is LocalBranch localBranch))
		{
			return;
		}
		string release = localBranch.Name.Remove(0, _gitFlowSettings.ReleasePrefix.Length);
		string tagMessage = TagMessageTextBox.Text;
			bool deleteBranches = DeleteBranchesCheckBox.IsChecked.GetValueOrDefault();
			bool noBackmerge = !BackMergeMasterCheckBox.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.GitFlowFinishRelease_DeleteBranches = deleteBranches;
			ForkPlusSettings.Default.GitFlowFinishRelease_BackMergeMaster = !noBackmerge;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Finishing '" + localBranch.Name + "'...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Finish '{0}'", localBranch.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new FinishGitFlowReleaseGitCommand().Execute(_gitModule, release, deleteBranches, noBackmerge, tagMessage, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void Refresh()
		{
			_gitFlowSettings = _repositoryData.GitFlowSettings;
			_allReleaseBranches = _repositoryData.References.LocalBranches.Filter((LocalBranch x) => x.Name.StartsWith(_gitFlowSettings.ReleasePrefix));
			BranchesComboBox.ItemsSource = _allReleaseBranches;
			BranchesComboBox.SelectedItem = _allReleaseBranches.FirstItem((LocalBranch x) => x.Name == _releaseBranch.Name);
			DeleteBranchesCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishRelease_DeleteBranches;
		BackMergeMasterCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishRelease_BackMergeMaster;
		RefreshCommandPreview();
	}

	private void BranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		RefreshCommandPreview();
	}

	private void CheckBox_Changed(object sender, RoutedEventArgs e)
	{
		RefreshCommandPreview();
	}

	}
}
