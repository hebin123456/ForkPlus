using Avalonia.Controls.Selection;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class RevertRevisionWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Revision _revision;

		private Sha[] _revisionParents;

	// 阶段 3：承接"merge 提交需选父提交"校验 + 命令预览。冲突预检/可见性切换留 View。
	private RevertRevisionWindowViewModel _viewModel;

		private bool MergeRevision => _revisionParents.Length > 1;

		protected override bool IsSubmitAllowed
		{
			get
			{
				_viewModel.Commit = CommitCheckBox.IsChecked.GetValueOrDefault();
				_viewModel.SelectedParentIndex = RevisionParentComboBox.SelectedIndex;
				return _viewModel.IsSubmitAllowed;
			}
		}

		public RevertRevisionWindow(RepositoryUserControl repositoryUserControl, Revision revision, Sha[] revisionParents)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			_repositoryUserControl = repositoryUserControl;
			_revision = revision;
			_revisionParents = revisionParents;
			_viewModel = new RevertRevisionWindowViewModel(_revision, _revisionParents);
			InitializeComponent();
			base.DialogTitle = Translate("Revert");
			base.DialogDescription = Translate("Revert changes of the individual commit");
			base.SubmitButtonTitle = Translate("Revert");
			RevisionGitPointView.Value = revision;
			CommitCheckBox.IsChecked = true;
			if (MergeRevision)
			{
				GitCommandResult<Revision[]> gitCommandResult = new GetRevisionsGitCommand().Execute(gitModule, _revisionParents);
				if (!gitCommandResult.Succeeded)
				{
					Log.Error(gitCommandResult.Error.FriendlyDescription);
					return;
				}
				Revision[] result = gitCommandResult.Result;
				if (result.Length <= 1)
				{
					return;
				}
				RevisionParentComboBox.ItemsSource = result;
				RevisionParentComboBox.SelectedIndex = 0;
				RevisionParentTextBlock.Visibility = Visibility.Visible;
				RevisionParentComboBox.Visibility = Visibility.Visible;
			}
			else
			{
				RevisionParentTextBlock.Visibility = Visibility.Collapsed;
				RevisionParentComboBox.Visibility = Visibility.Collapsed;
			}
			UpdateSubmitButton();
			// Revert 冲突预检：构造函数里同步调用 git merge-tree 做无副作用预演，
			// 三态展示（Success / Warning / Unknown 不显示）。
			int? previewParentNumber = (MergeRevision ? new int?(1) : null);
			GitCommandResult<RevertTestGitCommand.TestResult> previewResult = new RevertTestGitCommand().Execute(gitModule, _revision.Sha, previewParentNumber);
			if (previewResult.Succeeded)
			{
				if (previewResult.Result == RevertTestGitCommand.TestResult.Success)
				{
					SetStatus(ForkPlusDialogStatus.Success, Translate("Revert can be done without conflicts"));
				}
				else if (previewResult.Result == RevertTestGitCommand.TestResult.Conflict)
				{
					SetStatus(ForkPlusDialogStatus.Warning, Translate("Revert will cause conflicts"));
				}
			}
		}

		protected override string GetCommandPreview()
		{
			_viewModel.Commit = CommitCheckBox.IsChecked.GetValueOrDefault();
			_viewModel.SelectedParentIndex = RevisionParentComboBox.SelectedIndex;
			return _viewModel.CommandPreview;
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			Sha shaToRevert = _revision.Sha;
			bool commit = CommitCheckBox.IsChecked.GetValueOrDefault();
			int? parentNumber = (MergeRevision ? new int?(RevisionParentComboBox.SelectedIndex + 1) : null);
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			DisableEditableControls();
			_repositoryUserControl.AddUndoable(string.Format(Translate("Revert '{0}'"), shaToRevert.ToAbbreviatedString()), delegate(JobMonitor monitor)
		{
			base.Dispatcher.Async(delegate
			{
				SetStatus(ForkPlusDialogStatus.InProgress, Translate("Reverting..."));
			});
			GitCommandResult revertResult = new RevertCommitGitCommand().Execute(gitModule, shaToRevert, commit, parentNumber, monitor);
			GitCommandResult updateSubmodulesResult = GitCommandResult.Success();
			if (submodulesToUpdate.Length > 0)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Updating submodules...");
				});
				updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
			}
			base.Dispatcher.Async(delegate
			{
				if (!revertResult.Succeeded)
				{
					Close(revertResult);
				}
				else if (!updateSubmodulesResult.Succeeded)
				{
					Close(updateSubmodulesResult);
				}
				else
				{
					Close(revertResult);
				}
			});
			return revertResult.Succeeded ? updateSubmodulesResult : revertResult;
		}, JobFlags.SaveToLog);
	}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private void CommitCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private void RevisionParentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshCommandPreview();
		}

	}
}
