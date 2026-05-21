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
			base.DialogTitle = "Start Git Flow feature";
			base.DialogDescription = "Create a new feature branch based on 'develop' and switch to it";
			base.SubmitButtonTitle = "Start Feature";
			_gitModule = gitModule;
			Refresh();
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
				base.Dispatcher.Async(delegate
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
		}

		private void BranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void Refresh()
		{
			RepositoryData repositoryData = (Application.Current.MainWindow as MainWindow).TabManager.ActiveRepositoryUserControl.RepositoryData;
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
