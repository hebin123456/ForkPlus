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
	public partial class GitFlowFinishHotfixWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly LocalBranch _hotfixBranch;

		private readonly RepositoryData _repositoryData;

		private GitFlowSettings _gitFlowSettings;

		private IReadOnlyList<LocalBranch> _allHotfixBranches;

		public GitFlowFinishHotfixWindow(GitModule gitModule, RepositoryData repositoryData, LocalBranch hotfixBranch)
		{
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Finish Git Flow hotfix");
			base.DialogDescription = PreferencesLocalization.Current("Finish the hotfix and merge it into the develop and master branches");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Finish");
			_gitModule = gitModule;
			_repositoryData = repositoryData;
			_hotfixBranch = hotfixBranch;
			Refresh();
		}

		protected override string GetCommandPreview()
	{
		if (!(BranchesComboBox.SelectedItem is LocalBranch localBranch) || _gitFlowSettings == null)
		{
			return null;
		}
		string hotfix = localBranch.Name.Remove(0, _gitFlowSettings.HotfixPrefix.Length);
		return "git flow hotfix finish " + hotfix;
	}

	protected override void OnSubmit()
	{
		if (!(BranchesComboBox.SelectedItem is LocalBranch localBranch))
		{
			return;
		}
		string hotfix = localBranch.Name.Remove(0, _gitFlowSettings.HotfixPrefix.Length);
		string tagMessage = TagMessageTextBox.Text;
			bool deleteBranches = DeleteBranchesCheckBox.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.GitFlowFinishHotfix_DeleteBranches = deleteBranches;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Finishing '" + localBranch.Name + "'...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Finish '{0}'", localBranch.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new FinishGitFlowHotfixGitCommand().Execute(_gitModule, hotfix, deleteBranches, tagMessage, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void Refresh()
		{
			_gitFlowSettings = _repositoryData.GitFlowSettings;
			_allHotfixBranches = _repositoryData.References.LocalBranches.Filter((LocalBranch x) => x.Name.StartsWith(_gitFlowSettings.HotfixPrefix));
			BranchesComboBox.ItemsSource = _allHotfixBranches;
			BranchesComboBox.SelectedItem = _allHotfixBranches.FirstItem((LocalBranch x) => x.Name == _hotfixBranch.Name);
			DeleteBranchesCheckBox.IsChecked = ForkPlusSettings.Default.GitFlowFinishHotfix_DeleteBranches;
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
