using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitFlowStartReleaseWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private LocalBranch[] _localBranches;

		private GitFlowSettings _gitFlowSettings;

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				if (!(BranchesComboBox.SelectedItem is LocalBranch))
				{
					return false;
				}
				string text = ReleaseNameTextBox.Text;
				if (string.IsNullOrEmpty(text))
				{
					return false;
				}
				string text2 = ReferenceNameValidator.ValidateGitFlow(text);
				if (text2 != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text2);
					return false;
				}
				string branchName = (_gitFlowSettings.ReleasePrefix + text).ToLower();
				if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
				{
					SetStatus(ForkPlusDialogStatus.Warning, "Branch '" + branchName + "' already exists");
					return false;
				}
				return true;
			}
		}

		public GitFlowStartReleaseWindow(GitModule gitModule)
		{
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Start Git Flow release");
			base.DialogDescription = PreferencesLocalization.Current("Create a new release branch based on 'develop' and switch to it");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Start Release");
			_gitModule = gitModule;
			Refresh();
		}

		protected override string GetCommandPreview()
	{
		string releaseName = ReleaseNameTextBox.Text;
		if (string.IsNullOrWhiteSpace(releaseName))
		{
			return null;
		}
		LocalBranch baseBranch = BranchesComboBox.SelectedItem as LocalBranch;
		if (baseBranch == null)
		{
			return null;
		}
		return "git flow release start " + releaseName + " " + baseBranch.Name;
	}

	protected override void OnSubmit()
	{
		object selectedItem = BranchesComboBox.SelectedItem;
		LocalBranch startPoint = selectedItem as LocalBranch;
		if (startPoint == null)
		{
			return;
		}
		string releaseName = ReleaseNameTextBox.Text;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Starting '" + _gitFlowSettings.ReleasePrefix + releaseName + "'...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Start '{0}'", _gitFlowSettings.ReleasePrefix + releaseName), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new StartGitFlowReleaseGitCommand().Execute(_gitModule, releaseName, startPoint, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void ReleaseName_TextChanged(object sender, TextChangedEventArgs e)
	{
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

	private void BranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private void Refresh()
		{
			RepositoryData repositoryData = (Application.Current.MainWindow as MainWindow).TabManager.ActiveRepositoryUserControl.RepositoryData;
			_gitFlowSettings = repositoryData.GitFlowSettings;
			_localBranches = repositoryData.References.LocalBranches;
			BranchesComboBox.ItemsSource = _localBranches;
			BranchesComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.Name == _gitFlowSettings.DevelopBranch) ?? IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.IsActive);
			ReleasePrefixTextBlock.Text = _gitFlowSettings.ReleasePrefix;
		RefreshCommandPreview();
	}

	}
}
