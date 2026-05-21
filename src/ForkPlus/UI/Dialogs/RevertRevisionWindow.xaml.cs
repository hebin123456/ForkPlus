using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
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

		private bool MergeRevision => _revisionParents.Length > 1;

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
			InitializeComponent();
			base.DialogTitle = "Revert";
			base.DialogDescription = "Revert changes of the individual commit";
			base.SubmitButtonTitle = "Revert";
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
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Revert '{0}'"), shaToRevert.ToAbbreviatedString()), delegate(JobMonitor monitor)
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
			}, JobFlags.SaveToLog);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
