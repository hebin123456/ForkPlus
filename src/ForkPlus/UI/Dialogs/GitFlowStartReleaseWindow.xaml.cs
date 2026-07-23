using Avalonia.Controls.Selection;
using Avalonia.Controls;
using Avalonia.Threading;
using System;
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

	// 阶段 3：承接 base 分支选择 + release 名校验（GitFlow 格式 + 重名）+ 命令预览。
	// Validate() 返回 (IsAllowed, Status, StatusMessage)，View override 据此调 SetStatus。
	private GitFlowStartReleaseWindowViewModel _viewModel;

	protected override bool IsSubmitAllowed
	{
		get
		{
			_viewModel.ReleaseName = ReleaseNameTextBox.Text;
			_viewModel.SelectedBaseBranch = BranchesComboBox.SelectedItem as LocalBranch;
			(bool isAllowed, ForkPlusDialogStatus status, string statusMessage) = _viewModel.Validate();
			SetStatus(status, statusMessage ?? string.Empty);
			return isAllowed;
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
		_viewModel.ReleaseName = ReleaseNameTextBox.Text;
		_viewModel.SelectedBaseBranch = BranchesComboBox.SelectedItem as LocalBranch;
		return _viewModel.CommandPreview;
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
			_viewModel = new GitFlowStartReleaseWindowViewModel(_gitFlowSettings, _localBranches);
			BranchesComboBox.ItemsSource = _localBranches;
			BranchesComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.Name == _gitFlowSettings.DevelopBranch) ?? IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.IsActive);
			ReleasePrefixTextBlock.Text = _gitFlowSettings.ReleasePrefix;
		RefreshCommandPreview();
	}

	}
}
