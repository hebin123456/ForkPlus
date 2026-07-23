using Avalonia.Input;
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
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Dialogs
{
	public partial class CheckoutBranchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly LocalBranch _branch;

		private readonly RemoteBranch _fastForwardTo;

		protected override string GetCommandPreview()
		{
			LocalBranch branch = _branch;
			if (branch == null)
			{
				return null;
			}
			var parts = new System.Collections.Generic.List<string> { "git", "checkout" };
			if (DiscardRadioButton.IsChecked.GetValueOrDefault())
			{
				parts.Add("--force");
			}
			parts.Add(branch.Name);
			string command = string.Join(" ", parts);
			if (_repositoryUserControl != null && _repositoryUserControl.RepositoryStatus.WorkingDirectoryIsDirty() && StashAndReapplyRadioButton.IsChecked.GetValueOrDefault())
			{
				command = "git stash\n" + command;
			}
			return command;
		}

		public CheckoutBranchWindow(RepositoryUserControl repositoryUserControl, LocalBranch branch, RemoteBranch fastForwardTo)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_branch = branch;
			_fastForwardTo = fastForwardTo;
			GitPointView.Value = branch;
			if (fastForwardTo != null)
			{
				base.DialogTitle = PreferencesLocalization.Current("Checkout and Fast-Forward");
				base.DialogDescription = PreferencesLocalization.Current("Checkout local branch and fast-forward it to remote branch");
				base.SubmitButtonTitle = PreferencesLocalization.Current("Checkout and Fast-Forward");
				FastForwardGitPointView.Value = fastForwardTo;
				FastForwardTextBlock.Show();
				FastForwardGitPointView.Show();
			}
			else
			{
				base.DialogTitle = PreferencesLocalization.Current("Checkout Branch");
				base.DialogDescription = PreferencesLocalization.Current("Switch to another branch");
				base.SubmitButtonTitle = PreferencesLocalization.Current("Checkout");
				FastForwardTextBlock.Collapse();
				FastForwardGitPointView.Collapse();
			}
			bool checkout_StashAndReapply = ForkPlusSettings.Default.Checkout_StashAndReapply;
			StashAndReapplyRadioButton.IsChecked = checkout_StashAndReapply;
			DoNotChangeRadioButton.IsChecked = !checkout_StashAndReapply;
			if (repositoryUserControl.RepositoryStatus.WorkingDirectoryIsDirty())
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
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			LocalBranch branch = _branch;
			RemoteBranch fastForwardTo = _fastForwardTo;
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
		_repositoryUserControl.AddUndoable(PreferencesLocalization.FormatCurrent("Checkout branch '{0}'", branch.Name), delegate(JobMonitor monitor)
		{
			if (leaveAsStash)
			{
				GitCommandResult<bool> stashResult = new SaveStashGitCommand().Execute(gitModule, $"Autostash. Switch from '{sourceString}' to '{branch.Name}' {DateTime.Now}", stageNewFiles: false, monitor);
				if (!stashResult.Succeeded)
				{
					GitCommandResult failResult = GitCommandResult.Failure(stashResult.Error);
					base.Dispatcher.Async(delegate
					{
						Close(failResult);
					});
					return failResult;
				}
			}
			GitCommandResult result = PerformCheckout(gitModule, branch, fastForwardTo, checkoutStashAndReapply, checkoutDiscard, sourceString, submodulesToUpdate, monitor);
			if (monitor.IsCanceled)
			{
				Close(GitCommandResult.Success());
				return GitCommandResult.Success();
			}
			base.Dispatcher.Async(delegate
			{
				Close(result);
			});
			return result;
		}, JobFlags.SaveToLog);
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

		private GitCommandResult PerformCheckout(GitModule gitModule, LocalBranch branch, [Null] RemoteBranch fastForwardTo, StashAndReapply stashAndReapply, bool discardLocalChanges, string sourceString, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
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
				GitCommandResult<bool> gitCommandResult = new SaveStashGitCommand().Execute(gitModule, $"Autostash. Switch from '{sourceString}' to '{branch.Name}' {DateTime.Now}", stageNewFiles: false, monitor);
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
				SetStatus(ForkPlusDialogStatus.InProgress, PreferencesLocalization.Current("Checkout..."));
			});
			GitCommandResult gitCommandResult2 = new CheckoutBranchGitCommand().Execute(gitModule, branch, monitor, discardLocalChanges);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (!gitCommandResult2.Succeeded)
			{
				if (gitCommandResult2.Error is GitCommandError.CheckoutLocalChangesWouldBeOverwritten && stashAndReapply == StashAndReapply.Possible)
				{
					monitor.AppendOutputLine("fork: failed to checkout without overwriting local changes. Trying again with stash and reapply...\n");
					return PerformCheckout(gitModule, branch, fastForwardTo, StashAndReapply.Required, discardLocalChanges, sourceString, submodulesToUpdate, monitor);
				}
				UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
				return gitCommandResult2;
			}
			if (fastForwardTo != null)
			{
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				GitCommandResult gitCommandResult3 = new FastForwardMergeGitCommand().Execute(gitModule, fastForwardTo, monitor);
				if (!gitCommandResult3.Succeeded)
				{
					if (gitCommandResult3.Error is GitCommandError.MergeLocalChangesWouldBeOverwritten && stashAndReapply == StashAndReapply.Possible)
					{
						monitor.AppendOutputLine("fork: failed to fast-forward overwriting touching local changes. Trying again with stash and reapply...\n");
						return PerformCheckout(gitModule, branch, fastForwardTo, StashAndReapply.Required, discardLocalChanges, sourceString, submodulesToUpdate, monitor);
					}
					UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
					return gitCommandResult3;
				}
			}
			if (stashAndReapply == StashAndReapply.Required)
			{
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				GitCommandResult gitCommandResult4 = new ApplyStashGitCommand().Execute(gitModule, "stash@{0}", deleteAfterApply: true, monitor);
				if (!gitCommandResult4.Succeeded)
				{
					UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
					return gitCommandResult4;
				}
			}
			GitCommandResult gitCommandResult5 = UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
			if (!gitCommandResult5.Succeeded)
			{
				return gitCommandResult5;
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
