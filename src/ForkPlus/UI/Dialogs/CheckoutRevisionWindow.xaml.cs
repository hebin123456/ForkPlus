using System;
using System.ComponentModel;
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
	public partial class CheckoutRevisionWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly IGitPoint _gitPoint;

		private readonly Sha _gitPointSha;

		protected override string GetCommandPreview()
		{
			if (_gitPointSha == null)
			{
				return null;
			}
			var parts = new System.Collections.Generic.List<string> { "git", "checkout" };
			if (DiscardRadioButton.IsChecked.GetValueOrDefault())
			{
				parts.Add("--force");
			}
			parts.Add(_gitPointSha.ToAbbreviatedString());
			string command = string.Join(" ", parts);
			if (_repositoryUserControl != null && _repositoryUserControl.RepositoryStatus.WorkingDirectoryIsDirty() && StashAndReapplyRadioButton.IsChecked.GetValueOrDefault())
			{
				command = "git stash\n" + command;
			}
			return command;
		}

		public CheckoutRevisionWindow(RepositoryUserControl repositoryUserControl, IGitPoint gitPoint, Sha gitPointSha)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Checkout Commit");
			base.DialogDescription = Translate("Checkout a particular revision. Repository will be in detached HEAD state.");
			base.SubmitButtonTitle = Translate("Checkout Commit");
			_repositoryUserControl = repositoryUserControl;
			_gitPoint = gitPoint;
			_gitPointSha = gitPointSha;
			RevisionGitPointView.Value = gitPoint;
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
			IGitPoint gitPoint = _gitPoint;
			Sha gitPointSha = _gitPointSha;
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
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Checkout '{0}'"), gitPoint.FriendlyName), delegate(JobMonitor monitor)
			{
				if (leaveAsStash)
				{
					GitCommandResult<bool> stashResult = new SaveStashGitCommand().Execute(gitModule, $"Autostash. Switch from '{sourceString}' to '{gitPoint.FriendlyName}' (detached HEAD) {DateTime.Now}", stageNewFiles: false, monitor);
					if (!stashResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(GitCommandResult.Failure(stashResult.Error));
						});
						return;
					}
				}
				GitCommandResult result = PerformCheckout(gitModule, gitPoint, gitPointSha, checkoutStashAndReapply, checkoutDiscard, sourceString, submodulesToUpdate, monitor);
				if (monitor.IsCanceled)
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
			StashAndReapplyRadioButton.Content = Translate(KeyboardHelper.IsShiftDown ? "Leave as stash" : "Stash and reapply");
		}

		private GitCommandResult PerformCheckout(GitModule gitModule, IGitPoint gitPoint, Sha gitPointSha, StashAndReapply stashAndReapply, bool discardLocalChanges, string sourceString, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			monitor.SetState(JobMonitorState.InProgress);
			if (stashAndReapply == StashAndReapply.Required)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Stashing...");
				});
				if (monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				GitCommandResult<bool> gitCommandResult = new SaveStashGitCommand().Execute(gitModule, $"Autostash. Switch from '{sourceString}' to '{gitPoint.FriendlyName}' (detached HEAD) {DateTime.Now}", stageNewFiles: false, monitor);
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
				SetStatus(ForkPlusDialogStatus.InProgress, Translate("Checkout..."));
			});
			GitCommandResult gitCommandResult2 = new CheckoutRevisionGitCommand().Execute(gitModule, gitPointSha, monitor, discardLocalChanges);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (!gitCommandResult2.Succeeded)
			{
				if (gitCommandResult2.Error is GitCommandError.CheckoutLocalChangesWouldBeOverwritten && stashAndReapply == StashAndReapply.Possible)
				{
					monitor.AppendOutputLine("fork: failed to checkout without overwriting local changes. Trying again with stash and reapply...\n");
					return PerformCheckout(gitModule, gitPoint, gitPointSha, StashAndReapply.Required, discardLocalChanges, sourceString, submodulesToUpdate, monitor);
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
				SetStatus(ForkPlusDialogStatus.InProgress, "Updating submodules...");
			});
			return new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
