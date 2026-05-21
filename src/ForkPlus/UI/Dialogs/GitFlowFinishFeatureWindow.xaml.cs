using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitFlowFinishFeatureWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly LocalBranch _featureBranch;

		private readonly RepositoryData _repositoryData;

		private GitFlowSettings _gitFlowSettings;

		private IReadOnlyList<LocalBranch> _allFeatureBranches;

		public GitFlowFinishFeatureWindow(GitModule gitModule, RepositoryData repositoryData, LocalBranch featureBranch)
		{
			InitializeComponent();
			base.DialogTitle = "Finish Git Flow feature";
			base.DialogDescription = "Finish the feature and merge it into the develop branch";
			base.SubmitButtonTitle = "Finish";
			_gitModule = gitModule;
			_repositoryData = repositoryData;
			_featureBranch = featureBranch;
			Refresh();
		}

		protected override void OnSubmit()
		{
			if (!(BranchesComboBox.SelectedItem is LocalBranch localBranch))
			{
				return;
			}
			string feature = localBranch.Name.Remove(0, _gitFlowSettings.FeaturePrefix.Length);
			bool deleteBranches = DeleteBranchesCheckBox.IsChecked.GetValueOrDefault();
			bool rebase = RebaseInsteadOfMergeCheckBox.IsChecked.GetValueOrDefault();
			bool noFastForward = NoFastForwardCheckBox.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.GitFlowFinishFeature_DeleteBranches = deleteBranches;
			ForkPlusSettings.Default.GitFlowFinishFeature_Rebase = rebase;
			ForkPlusSettings.Default.GitFlowFinishFeature_NoFastForward = noFastForward;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Finishing '" + localBranch.Name + "'...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Finish '{0}'", localBranch.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new FinishGitFlowFeatureGitCommand().Execute(_gitModule, feature, rebase, deleteBranches, noFastForward, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void Refresh()
		{
			_gitFlowSettings = _repositoryData.GitFlowSettings;
			_allFeatureBranches = _repositoryData.References.LocalBranches.Filter((LocalBranch x) => x.Name.StartsWith(_gitFlowSettings.FeaturePrefix));
			BranchesComboBox.ItemsSource = _allFeatureBranches;
			BranchesComboBox.SelectedItem = _allFeatureBranches.FirstItem((LocalBranch x) => x.Name == _featureBranch.Name);
			DeleteBranchesCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishFeature_DeleteBranches;
			RebaseInsteadOfMergeCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishFeature_Rebase;
			NoFastForwardCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishFeature_NoFastForward;
		}

	}
}
