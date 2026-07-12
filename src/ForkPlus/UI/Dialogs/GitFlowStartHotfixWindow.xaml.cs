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
	public partial class GitFlowStartHotfixWindow : ForkPlusDialogWindow
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
				string text = HotfixNameTextBox.Text;
				if (string.IsNullOrEmpty(text))
				{
					return false;
				}
				string text2 = ReferenceNameValidator.ValidateGitFlow(text);
				if (text2 != null)
				{
					SetStatus(ForkPlusDialogStatus.Error, text2);
					return false;
				}
				string branchName = (_gitFlowSettings.HotfixPrefix + text).ToLower();
				if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
				{
					SetStatus(ForkPlusDialogStatus.Warning, "Branch '" + branchName + "' already exists");
					return false;
				}
				return true;
			}
		}

		public GitFlowStartHotfixWindow(GitModule gitModule)
		{
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Start Git Flow hotfix");
			base.DialogDescription = PreferencesLocalization.Current("Create a new hotfix branch based on 'master' and switch to it");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Start Hotfix");
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
			string hotfixName = HotfixNameTextBox.Text;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, "Starting '" + _gitFlowSettings.HotfixPrefix + hotfixName + "'...");
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Start '{0}'", _gitFlowSettings.HotfixPrefix + hotfixName), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new StartGitFlowHotfixGitCommand().Execute(_gitModule, hotfixName, startPoint, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private void HotfixName_TextChanged(object sender, TextChangedEventArgs e)
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
			BranchesComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.Name == _gitFlowSettings.MasterBranch) ?? IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.IsActive);
			HotfixPrefixTextBlock.Text = _gitFlowSettings.HotfixPrefix;
		}

	}
}
