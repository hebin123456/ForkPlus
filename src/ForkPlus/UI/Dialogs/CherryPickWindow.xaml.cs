using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
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

		private bool MergeRevision => _firstRevisionParents.Length > 1;

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (MergeRevision)
				{
					return RevisionParentComboBox.SelectedItem != null;
				}
				return true;
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
			if (_revisions == null || _revisions.Length == 0)
			{
				return null;
			}
			var parts = new System.Collections.Generic.List<string> { "git", "cherry-pick" };
			bool commit = CommitCheckBox.IsChecked.GetValueOrDefault();
			bool appendOriginSha = AppendOriginShaCheckBox.IsChecked.GetValueOrDefault();
			if (!commit)
			{
				parts.Add("--no-commit");
			}
			if (appendOriginSha)
			{
				parts.Add("-x");
			}
			if (MergeRevision)
			{
				int parentNumber = RevisionParentComboBox.SelectedIndex + 1;
				if (parentNumber > 0)
				{
					parts.Add("-m " + parentNumber.ToString());
				}
			}
			Sha[] shas = _revisions.Map((Revision x) => x.Sha);
			Array.Reverse(shas);
			foreach (Sha sha in shas)
			{
				parts.Add(sha.ToAbbreviatedString());
			}
			return string.Join(" ", parts);
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
