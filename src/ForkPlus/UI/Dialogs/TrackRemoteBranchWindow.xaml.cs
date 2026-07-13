using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Dialogs
{
	public partial class TrackRemoteBranchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly LocalBranch[] _localBranches;

		private readonly RemoteBranch _remoteBranch;

		protected override string GetCommandPreview()
		{
			string localName = LocalBranchNameTextBox.Text;
			if (string.IsNullOrWhiteSpace(localName))
			{
				return null;
			}
			RemoteBranch remoteBranch = _remoteBranch;
			if (remoteBranch == null)
			{
				return null;
			}
			var parts = new System.Collections.Generic.List<string> { "git", "checkout" };
			if (DiscardRadioButton.IsChecked.GetValueOrDefault())
			{
				parts.Add("--force");
			}
			parts.Add("-b");
			parts.Add(localName);
			parts.Add(remoteBranch.Name);
			string command = string.Join(" ", parts);
			if (_repositoryUserControl != null && _repositoryUserControl.RepositoryStatus.WorkingDirectoryIsDirty() && StashAndReapplyRadioButton.IsChecked.GetValueOrDefault())
			{
				command = "git stash\n" + command;
			}
			return command;
		}

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				string branchName = LocalBranchNameTextBox.Text.ToLower();
				if (string.IsNullOrEmpty(branchName))
				{
					return false;
				}
				string text = ReferenceNameValidator.Validate(branchName);
				if (text != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text);
					return false;
				}
				if (_localBranches.Any((LocalBranch x) => x.Name.ToLower() == branchName))
				{
					SetStatus(ForkPlusDialogStatus.Warning, PreferencesLocalization.FormatCurrent("Branch '{0}' already exists", LocalBranchNameTextBox.Text));
					return false;
				}
				return true;
			}
		}

		public TrackRemoteBranchWindow(RepositoryUserControl repositoryUserControl, LocalBranch[] localBranches, RemoteBranch remoteBranch)
		{
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Track Remote Branch");
			base.DialogDescription = PreferencesLocalization.Current("Create new local branch which tracks remote branch");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Track");
			_repositoryUserControl = repositoryUserControl;
			_localBranches = localBranches;
			_remoteBranch = remoteBranch;
			GitPointView.Value = remoteBranch;
			LocalBranchNameTextBox.Text = _remoteBranch.ShortName;
			bool checkout_StashAndReapply = ForkPlusSettings.Default.Checkout_StashAndReapply;
			StashAndReapplyRadioButton.IsChecked = checkout_StashAndReapply;
			DoNotChangeRadioButton.IsChecked = !checkout_StashAndReapply;
			if (_repositoryUserControl.RepositoryStatus.WorkingDirectoryIsDirty())
			{
				LocalChangesTextBlock.Show();
				LocalChangesOptionsContainer.Show();
			}
			else
			{
				LocalChangesTextBlock.Collapse();
				LocalChangesOptionsContainer.Collapse();
			}
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
				{
					OnShiftKeyDown();
				}
			};
			base.KeyUp += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
				{
					OnShiftKeyUp();
				}
			};
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryReferences repositoryReferences = _repositoryUserControl.RepositoryData?.References;
			if (repositoryReferences == null)
			{
				return;
			}
			string localBranchName = LocalBranchNameTextBox.Text;
			RemoteBranch remoteBranch = _remoteBranch;
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			string sourceString = repositoryReferences.ActiveBranch?.Name ?? repositoryReferences.HeadSha?.ToAbbreviatedString() ?? "";
			StashAndReapply checkoutStashAndReapply;
			bool checkoutDiscard;
			bool leaveAsStash;
			if (StashAndReapplyRadioButton.IsChecked.GetValueOrDefault())
			{
				if (KeyboardHelper.IsShiftDown)
				{
					checkoutStashAndReapply = StashAndReapply.Forbidden;
					checkoutDiscard = false;
					leaveAsStash = true;
				}
				else
				{
					checkoutStashAndReapply = StashAndReapply.Possible;
					checkoutDiscard = false;
					leaveAsStash = false;
				}
			}
			else if (DoNotChangeRadioButton.IsChecked.GetValueOrDefault())
			{
				checkoutStashAndReapply = StashAndReapply.Forbidden;
				checkoutDiscard = false;
				leaveAsStash = false;
			}
			else
			{
				if (!DiscardRadioButton.IsChecked.GetValueOrDefault())
				{
					return;
				}
				checkoutStashAndReapply = StashAndReapply.Forbidden;
				checkoutDiscard = true;
				leaveAsStash = false;
			}
			ForkPlusSettings.Default.Checkout_StashAndReapply = checkoutStashAndReapply == StashAndReapply.Possible;
			ForkPlusSettings.Default.Save();
			DisableEditableControls();
			_repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Track '{0}'", remoteBranch.Name), delegate(JobMonitor monitor)
			{
				if (leaveAsStash)
				{
					GitCommandResult<bool> stashResult = new SaveStashGitCommand().Execute(gitModule, $"Autostash. Switch from '{sourceString}' to '{localBranchName}' {DateTime.Now}", stageNewFiles: false, monitor);
					if (!stashResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(GitCommandResult.Failure(stashResult.Error));
						});
						return;
					}
				}
				GitCommandResult result = PerformTrackBranch(gitModule, remoteBranch, localBranchName, checkoutStashAndReapply, checkoutDiscard, sourceString, submodulesToUpdate, monitor);
				if (result.Error is GitCommandError.Cancelled)
				{
					Close(GitCommandResult.Success());
				}
				else
				{
					base.Dispatcher.Async(delegate
					{
						Close(result);
					});
				}
			}, JobFlags.SaveToLog);
		}

		private void LocalBranchName_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void LocalChangesOption_Changed(object sender, RoutedEventArgs e)
		{
			if (DiscardRadioButton.IsChecked.GetValueOrDefault())
			{
				DiscardWarningImage.Show();
			}
			else
			{
				DiscardWarningImage.Hide();
			}
			RefreshCommandPreview();
		}

		private void OnShiftKeyDown()
		{
			UpdateStashAndReapplyRadioButtonTitle();
		}

		private void OnShiftKeyUp()
		{
			UpdateStashAndReapplyRadioButtonTitle();
		}

		private void UpdateStashAndReapplyRadioButtonTitle()
		{
			StashAndReapplyRadioButton.Content = PreferencesLocalization.Current(KeyboardHelper.IsShiftDown ? "Leave as stash" : "Stash and reapply");
		}

		private GitCommandResult PerformTrackBranch(GitModule gitModule, RemoteBranch remoteBranch, string localBranchName, StashAndReapply stashAndReapply, bool discardLocalChanges, string sourceString, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			monitor.SetState(JobMonitorState.InProgress);
			if (stashAndReapply == StashAndReapply.Required)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, PreferencesLocalization.Current("Stashing..."));
				});
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				GitCommandResult<bool> gitCommandResult = new SaveStashGitCommand().Execute(gitModule, $"Autostash. Switch from '{sourceString}' to '{localBranchName}' {DateTime.Now}", stageNewFiles: false, monitor);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult.Failure(gitCommandResult.Error);
				}
			}
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			base.Dispatcher.Async(delegate
			{
				SetStatus(ForkPlusDialogStatus.InProgress, PreferencesLocalization.FormatCurrent("Tracking '{0}'...", remoteBranch.Name));
			});
			GitCommandResult gitCommandResult2 = new CreateLocalAndTrackRemoteBranchGitCommand().Execute(gitModule, remoteBranch, localBranchName, monitor, discardLocalChanges);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (!gitCommandResult2.Succeeded)
			{
				if (gitCommandResult2.Error is GitCommandError.CheckoutLocalChangesWouldBeOverwritten && stashAndReapply == StashAndReapply.Possible)
				{
					monitor.AppendOutputLine("fork: failed to checkout without overwriting local changes. Trying again with stash and reapply...\n");
					return PerformTrackBranch(gitModule, remoteBranch, localBranchName, StashAndReapply.Required, discardLocalChanges, sourceString, submodulesToUpdate, monitor);
				}
				UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
				return gitCommandResult2;
			}
			if (stashAndReapply == StashAndReapply.Required)
			{
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				GitCommandResult gitCommandResult3 = new ApplyStashGitCommand().Execute(gitModule, "stash@{0}", deleteAfterApply: true, monitor);
				if (!gitCommandResult3.Succeeded)
				{
					UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
					return gitCommandResult3;
				}
			}
			GitCommandResult gitCommandResult4 = UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
			if (!gitCommandResult4.Succeeded)
			{
				return gitCommandResult4;
			}
			return gitCommandResult2;
		}

		private GitCommandResult UpdateSubmodulesIfNeeded(GitModule gitModule, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			if (submodulesToUpdate.Length == 0)
			{
				return GitCommandResult.Success();
			}
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			base.Dispatcher.Async(delegate
			{
				SetStatus(ForkPlusDialogStatus.InProgress, PreferencesLocalization.Current("Updating submodules..."));
			});
			return new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
		}

	}
}
