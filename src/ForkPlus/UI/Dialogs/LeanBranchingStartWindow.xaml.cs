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
	public partial class LeanBranchingStartWindow : ForkPlusDialogWindow
	{
		[Null]
		private static string UnfinishedBranchName;

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Branch _mainBranch;

		private readonly LocalBranch[] _localBranches;

		private readonly RepositoryReferences _repositoryReferences;

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				string branchName = BranchNameTextBox.Text.ToLower();
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
				if (_localBranches.AnyItem((LocalBranch x) => x.Name.ToLower() == branchName))
				{
					SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("Branch '{0}' already exists"), BranchNameTextBox.Text));
					return false;
				}
				return true;
			}
		}

		public LeanBranchingStartWindow(RepositoryUserControl repositoryUserControl, Branch mainBranch)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Start Branch");
			base.DialogDescription = Translate("Use '/' as a path separator to create folders");
			_repositoryUserControl = repositoryUserControl;
			_repositoryReferences = repositoryUserControl.RepositoryData.References;
			_localBranches = _repositoryReferences.LocalBranches;
			_mainBranch = mainBranch;
			GitPointView.Value = mainBranch;
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
			ReferenceTextBox branchNameTextBox = BranchNameTextBox;
			ForkPlus.Git.Reference[] references = _repositoryReferences.Items.CompactMap((ForkPlus.Git.Reference x) => x as Branch);
			branchNameTextBox.SetAutocompleteProvider(new ReferenceNameAutocompleteProvider(references));
			if (UnfinishedBranchName != null)
			{
				BranchNameTextBox.Text = UnfinishedBranchName;
				BranchNameTextBox.SelectAll();
			}
			else
			{
				string recentNewBranchPrefix = repositoryUserControl.GitModule.Settings.RecentNewBranchPrefix;
				if (recentNewBranchPrefix != null)
				{
					BranchNameTextBox.Text = recentNewBranchPrefix;
					BranchNameTextBox.SelectAll();
				}
			}
			UpdateSubmitButtonTitle();
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
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			Branch mainBranch = _mainBranch;
			bool checkout = true;
			string branchName = BranchNameTextBox.Text;
			string sourceString = _repositoryReferences.ActiveBranch?.Name ?? _repositoryReferences.HeadSha?.ToAbbreviatedString() ?? "";
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
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Creating branch '{0}'"), branchName), delegate(JobMonitor monitor)
			{
				if (checkout && leaveAsStash)
				{
					GitCommandResult<bool> stashResult = new SaveStashGitCommand().Execute(gitModule, $"Autostash. Switch from '{sourceString}' to '{branchName}' {DateTime.Now}", stageNewFiles: false, monitor);
					if (!stashResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(GitCommandResult.Failure(stashResult.Error));
						});
						return;
					}
				}
				GitCommandResult result = PerformCreateBranch(checkout, gitModule, mainBranch, branchName, checkoutStashAndReapply, checkoutDiscard, sourceString, submodulesToUpdate, monitor);
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

		private void BranchName_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
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

		private GitCommandResult PerformCreateBranch(bool checkout, GitModule gitModule, Branch mainBranch, string branchName, StashAndReapply stashAndReapply, bool discardLocalChanges, string sourceString, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
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
				GitCommandResult<bool> gitCommandResult = new SaveStashGitCommand().Execute(gitModule, $"Autostash. Switch from '{sourceString}' to '{branchName}' {DateTime.Now}", stageNewFiles: false, monitor);
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
				SetStatus(ForkPlusDialogStatus.InProgress, Translate("Creating branch..."));
			});
			GitCommandResult gitCommandResult2 = new CreateNewBranchGitCommand().Execute(gitModule, branchName, checkout, mainBranch, monitor, discardLocalChanges);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (!gitCommandResult2.Succeeded)
			{
				if (gitCommandResult2.Error is GitCommandError.CheckoutLocalChangesWouldBeOverwritten && stashAndReapply == StashAndReapply.Possible)
				{
					return PerformCreateBranch(checkout, gitModule, mainBranch, branchName, StashAndReapply.Required, discardLocalChanges, sourceString, submodulesToUpdate, monitor);
				}
				base.Dispatcher.Async(delegate
				{
					SaveUnfinishedBranchName();
				});
				UpdateSubmodulesIfNeeded(gitModule, submodulesToUpdate, monitor);
				return gitCommandResult2;
			}
			base.Dispatcher.Async(delegate
			{
				ClearUnfinishedBranchName();
				SaveRecentNewBranchPrefix(gitModule, branchName);
			});
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

		private void UpdateSubmitButtonTitle()
		{
			base.SubmitButtonTitle = Translate("Create");
		}

		private void SaveUnfinishedBranchName()
		{
			UnfinishedBranchName = BranchNameTextBox.Text;
		}

		private void ClearUnfinishedBranchName()
		{
			UnfinishedBranchName = null;
		}

		private void SaveRecentNewBranchPrefix(GitModule gitModule, string branchName)
		{
			int num = branchName.LastIndexOf("/");
			if (num != -1)
			{
				gitModule.Settings.RecentNewBranchPrefix = branchName.Substring(0, num + 1);
			}
			else
			{
				gitModule.Settings.RecentNewBranchPrefix = null;
			}
			gitModule.Settings.Save();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
