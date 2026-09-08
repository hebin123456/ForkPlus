using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	public partial class GitFlowStartFeatureWindow : ForkPlusDialogWindow
	{
		[Null]
		private static string UnfinishedBranchName;

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
				string text = FeatureNameTextBox.Text;
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
				string branchName = (_gitFlowSettings.FeaturePrefix + text).ToLower();
				if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
				{
					SetStatus(ForkPlusDialogStatus.Warning, "Branch '" + branchName + "' already exists");
					return false;
				}
				return true;
			}
		}

		public GitFlowStartFeatureWindow(GitModule gitModule)
		{
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Start Git Flow feature");
			base.DialogDescription = PreferencesLocalization.Current("Create a new feature branch based on 'develop' and switch to it");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Start Feature");
			_gitModule = gitModule;
			Refresh();
		}

		protected override string GetCommandPreview()
	{
		string featureName = FeatureNameTextBox.Text;
		if (string.IsNullOrWhiteSpace(featureName))
		{
			return null;
		}
		LocalBranch baseBranch = BranchesComboBox.SelectedItem as LocalBranch;
		if (baseBranch == null)
		{
			return null;
		}
		return "git flow feature start " + featureName + " " + baseBranch.Name;
	}

	protected override void OnSubmit()
	{
		object selectedItem = BranchesComboBox.SelectedItem;
		LocalBranch startPoint = selectedItem as LocalBranch;
		if (startPoint == null)
		{
			return;
		}
		string featureName = FeatureNameTextBox.Text;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Starting '" + _gitFlowSettings.FeaturePrefix + featureName + "'...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Start '{0}'", _gitFlowSettings.FeaturePrefix + featureName), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new StartGitFlowFeatureGitCommand().Execute(_gitModule, featureName, startPoint, monitor);
				base.Dispatcher.Post(delegate
				{
					if (!result.Succeeded)
					{
						SaveUnfinishedBranchName();
					}
					else
					{
						ClearUnfinishedBranchName();
					}
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void FeatureName_TextChanged(object sender, TextChangedEventArgs e)
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
			RepositoryData repositoryData = ((global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow).TabManager.ActiveRepositoryUserControl.RepositoryData;
			_gitFlowSettings = repositoryData.GitFlowSettings;
			_localBranches = repositoryData.References.LocalBranches;
			BranchesComboBox.ItemsSource = _localBranches;
			BranchesComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.Name == _gitFlowSettings.DevelopBranch) ?? IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.IsActive);
			FeaturePrefixTextBlock.Text = _gitFlowSettings.FeaturePrefix;
			if (UnfinishedBranchName != null)
		{
			FeatureNameTextBox.Text = UnfinishedBranchName;
			FeatureNameTextBox.SelectAll();
		}
		RefreshCommandPreview();
	}

		private void SaveUnfinishedBranchName()
		{
			UnfinishedBranchName = FeatureNameTextBox.Text;
		}

		private void ClearUnfinishedBranchName()
		{
			UnfinishedBranchName = null;
		}

	}
}
