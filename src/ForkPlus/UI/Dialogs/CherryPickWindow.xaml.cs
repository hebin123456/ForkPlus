using Avalonia.Controls.Selection;
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
	public partial class CherryPickWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Revision[] _revisions;

		private Sha[] _firstRevisionParents;

		// 阶段 3：承接 cherry-pick 父提交选择 + commit/-x 选项 + 多 sha 命令预览。
		// 合并提交（parents.Length>1）必须选 parent；非合并提交恒允许。
		// 多 commit shas 反转后逐个 ToAbbreviatedString 拼接预览。
		private readonly CherryPickWindowViewModel _viewModel;

		private bool MergeRevision => _firstRevisionParents.Length > 1;

		protected override bool IsSubmitAllowed
		{
			get
			{
				_viewModel.SelectedParentIndex = RevisionParentComboBox.SelectedIndex;
				return _viewModel.IsSubmitAllowed;
			}
		}

		public CherryPickWindow(RepositoryUserControl repositoryUserControl, Revision[] revisions, Sha[] firstRevisionParents)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			_repositoryUserControl = repositoryUserControl;
			_revisions = revisions;
			_firstRevisionParents = firstRevisionParents;
			_viewModel = new CherryPickWindowViewModel(_revisions, _firstRevisionParents);
			InitializeComponent();
			base.DialogTitle = Translate("Cherry Pick");
			base.DialogDescription = Translate("Apply changes of the individual commit");
			AppendOriginShaCheckBox.IsChecked = ForkPlusSettings.Default.CherryPick_AppendOriginSha;
			if (_revisions.SingleItem() != null)
			{
				GitPointTextBlock.Text = Translate("Commit to apply:");
				GitPointsContainer.Collapse();
				RevisionGitPointView.Show();
				RevisionGitPointView.Value = _revisions.FirstItem();
				if (MergeRevision)
				{
					GitCommandResult<Revision[]> gitCommandResult = new GetRevisionsGitCommand().Execute(gitModule, _firstRevisionParents);
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
					RevisionParentTextBlock.Show();
					RevisionParentComboBox.Show();
				}
				else
				{
					RevisionParentTextBlock.Collapse();
					RevisionParentComboBox.Collapse();
				}
				base.SubmitButtonTitle = Translate("Cherry Pick");
			}
			else
			{
				GitPointTextBlock.Text = Translate("Commits to apply:");
				RevisionGitPointView.Collapse();
				RevisionParentTextBlock.Collapse();
				RevisionParentComboBox.Collapse();
				GitPointsContainer.Show();
				GitPoints.ItemsSource = _revisions;
				base.SubmitButtonTitle = string.Format(Translate("Cherry Pick {0} commits"), _revisions.Length);
			}
			CommitCheckBox.IsChecked = true;
			UpdateSubmitButton();
			// Cherry-pick 冲突预检：构造函数里同步调用 git merge-tree 做无副作用预演，
			// 三态展示（Success / Warning / Unknown 不显示）。
			// 多 commit 场景下用第一个 commit 的预检结果代表整体（简化策略）。
			Sha[] previewShas = _revisions.Map((Revision x) => x.Sha);
			int? previewParentNumber = (MergeRevision ? new int?(1) : null);
			GitCommandResult<CherryPickTestGitCommand.TestResult> previewResult = new CherryPickTestGitCommand().Execute(gitModule, previewShas, previewParentNumber);
			if (previewResult.Succeeded)
			{
				if (previewResult.Result == CherryPickTestGitCommand.TestResult.Success)
				{
					SetStatus(ForkPlusDialogStatus.Success, Translate("Cherry-pick can be done without conflicts"));
				}
				else if (previewResult.Result == CherryPickTestGitCommand.TestResult.Conflict)
				{
					SetStatus(ForkPlusDialogStatus.Warning, Translate("Cherry-pick will cause conflicts"));
				}
			}
		}

		private void CommitCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshAppendOriginShaCheckBox();
			RefreshCommandPreview();
		}

		protected override string GetCommandPreview()
		{
			_viewModel.Commit = CommitCheckBox.IsChecked.GetValueOrDefault();
			_viewModel.AppendOriginSha = AppendOriginShaCheckBox.IsChecked.GetValueOrDefault();
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
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			Sha[] shas = _revisions.Map((Revision x) => x.Sha);
			Array.Reverse(shas);
			bool commit = CommitCheckBox.IsChecked.GetValueOrDefault();
			bool appendOriginSha = AppendOriginShaCheckBox.IsChecked.GetValueOrDefault();
			int? parentNumber = (MergeRevision ? new int?(RevisionParentComboBox.SelectedIndex + 1) : null);
			ForkPlusSettings.Default.CherryPick_AppendOriginSha = appendOriginSha;
			DisableEditableControls();
			_repositoryUserControl.AddUndoable(Translate("Cherry-pick"), delegate(JobMonitor monitor)
		{
			base.Dispatcher.Async(delegate
			{
				SetStatus(ForkPlusDialogStatus.InProgress, Translate("Cherry-picking..."));
			});
			GitCommandResult cherryPickResult = new CherryPickGitCommand().Execute(gitModule, shas, commit, appendOriginSha, parentNumber, monitor);
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
				if (!cherryPickResult.Succeeded)
				{
					Close(cherryPickResult);
				}
				else if (!updateSubmodulesResult.Succeeded)
				{
					Close(updateSubmodulesResult);
				}
				else
				{
					Close(cherryPickResult);
				}
			});
			return cherryPickResult.Succeeded ? updateSubmodulesResult : cherryPickResult;
		}, JobFlags.SaveToLog);
	}

		private void RefreshAppendOriginShaCheckBox()
		{
			if (CommitCheckBox.IsChecked.GetValueOrDefault())
			{
				AppendOriginShaCheckBox.Enable();
				return;
			}
			AppendOriginShaCheckBox.IsChecked = false;
			AppendOriginShaCheckBox.Disable();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private void AppendOriginShaCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private void RevisionParentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshCommandPreview();
		}

	}
}
