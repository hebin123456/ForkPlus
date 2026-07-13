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
	public partial class CheckoutAndSyncWindow : ForkPlusDialogWindow
	{
		public class CheckoutSyncOptionComboBoxItem : INotifyPropertyChanged
		{
			public CheckoutActionType? ActionType { get; }

			public string Title { get; }

			public string Description { get; }

			public bool IsSeparator { get; }

			public event PropertyChangedEventHandler PropertyChanged;

			private CheckoutSyncOptionComboBoxItem(string title, string description, CheckoutActionType? actionType, bool isSeparator)
			{
				ActionType = actionType;
				Title = title;
				Description = description;
				IsSeparator = isSeparator;
			}

			public CheckoutSyncOptionComboBoxItem(string title, string description, CheckoutActionType? actionType)
				: this(title, description, actionType, isSeparator: false)
			{
			}

			public static CheckoutSyncOptionComboBoxItem Separator()
			{
				return new CheckoutSyncOptionComboBoxItem("", "", null, isSeparator: true);
			}
		}

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly LocalBranch _localBranch;

		private readonly RemoteBranch _remoteBranch;

		private readonly CheckoutSyncOptionComboBoxItem[] _checkoutSyncOptionComboBoxItems;

		public CheckoutActionType _actionType;

		public CheckoutAndSyncWindow(RepositoryUserControl repositoryUserControl, LocalBranch localBranch, RemoteBranch remoteBranch)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_localBranch = localBranch;
			_remoteBranch = remoteBranch;
			_checkoutSyncOptionComboBoxItems = new CheckoutSyncOptionComboBoxItem[6]
			{
				new CheckoutSyncOptionComboBoxItem("Checkout", "Checkout '" + localBranch.Name + "'", CheckoutActionType.None),
				CheckoutSyncOptionComboBoxItem.Separator(),
				new CheckoutSyncOptionComboBoxItem("Rebase", "Rebase '" + localBranch.Name + "' onto '" + remoteBranch.Name + "'", CheckoutActionType.Rebase),
				new CheckoutSyncOptionComboBoxItem("Merge", "Merge '" + remoteBranch.Name + "' into '" + localBranch.Name + "'", CheckoutActionType.Merge),
				CheckoutSyncOptionComboBoxItem.Separator(),
				new CheckoutSyncOptionComboBoxItem("Reset", "Reset (--hard) '" + localBranch.Name + "' to '" + remoteBranch.Name + "'", CheckoutActionType.Reset)
			};
			CheckoutActionTypeComboBox.ItemsSource = _checkoutSyncOptionComboBoxItems;
			CheckoutActionTypeComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_checkoutSyncOptionComboBoxItems, (CheckoutSyncOptionComboBoxItem x) => x.ActionType == CheckoutActionType.None);
			base.DialogTitle = PreferencesLocalization.Current("Checkout Branch");
			base.DialogDescription = PreferencesLocalization.FormatCurrent("Switch to '{0}' branch", localBranch.Name);
			GitPointView.Value = localBranch;
		base.SubmitButtonTitle = PreferencesLocalization.Current("Checkout");
		RefreshCommandPreview();
	}

		protected override string GetCommandPreview()
	{
		if (_localBranch == null || string.IsNullOrEmpty(_localBranch.Name))
		{
			return null;
		}
		System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
		lines.Add("git checkout " + _localBranch.Name);
		if (_remoteBranch != null && !string.IsNullOrEmpty(_remoteBranch.Name))
		{
			switch (_actionType)
			{
			case CheckoutActionType.Rebase:
				lines.Add("git rebase " + _remoteBranch.Name);
				break;
			case CheckoutActionType.Merge:
				lines.Add("git merge " + _remoteBranch.Name);
				break;
			case CheckoutActionType.Reset:
				lines.Add("git reset --hard " + _remoteBranch.Name);
				break;
			}
		}
		return string.Join("\n", lines);
	}

	protected override void OnSubmit()
	{
		GitModule gitModule = _repositoryUserControl.GitModule;
		if (gitModule == null)
		{
			return;
		}
			RepositoryStatus repositoryStatus = _repositoryUserControl.RepositoryStatus;
			if (repositoryStatus == null)
			{
				return;
			}
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			LocalBranch localBranch = _localBranch;
			RemoteBranch remoteBranch = _remoteBranch;
			CheckoutActionType actionType = _actionType;
			bool stashAndReapply = AutostashCheckBox.IsChecked.GetValueOrDefault();
			bool workingDirectoryIsDirty = repositoryStatus.WorkingDirectoryIsDirty();
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			ForkPlusSettings.Default.CheckoutAndSync_StashAndReapply = stashAndReapply;
			ForkPlusSettings.Default.Save();
			DisableEditableControls();
			_repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Checkout branch '{0}'", localBranch.Name), delegate(JobMonitor monitor)
			{
				if (stashAndReapply && workingDirectoryIsDirty)
				{
					base.Dispatcher.Async(delegate
					{
						SetStatus(ForkPlusDialogStatus.InProgress, "Stashing...");
					});
					GitCommandResult<bool> stashResult = new SaveStashGitCommand().Execute(gitModule, $"Checkout autostash {DateTime.Now}", stageNewFiles: false, monitor);
					if (!stashResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(GitCommandResult.Failure(stashResult.Error));
						});
						return;
					}
				}
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Checkout...");
				});
				GitCommandResult checkoutResult = new CheckoutBranchGitCommand().Execute(gitModule, localBranch, monitor);
				if (!checkoutResult.Succeeded && !monitor.IsCanceled)
				{
					if (submodulesToUpdate.Length > 0)
					{
						base.Dispatcher.Async(delegate
						{
							SetStatus(ForkPlusDialogStatus.InProgress, "Updating submodules...");
						});
						new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
					}
					base.Dispatcher.Async(delegate
					{
						Close(checkoutResult);
					});
				}
				else
				{
					GitCommandResult actionResult = PerformAction(gitModule, remoteBranch, actionType, repositoryData.References, monitor);
					if (!actionResult.Succeeded)
					{
						if (submodulesToUpdate.Length > 0)
						{
							base.Dispatcher.Async(delegate
							{
								SetStatus(ForkPlusDialogStatus.InProgress, "Updating submodules...");
							});
							new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
						}
						base.Dispatcher.Async(delegate
						{
							Close(actionResult);
						});
					}
					else
					{
						GitCommandResult applyStashResult = GitCommandResult.Success();
						if (stashAndReapply && workingDirectoryIsDirty)
						{
							base.Dispatcher.Async(delegate
							{
								SetStatus(ForkPlusDialogStatus.InProgress, "Applying stash...");
							});
							applyStashResult = new ApplyStashGitCommand().Execute(gitModule, "stash@{0}", deleteAfterApply: true, monitor);
						}
						GitCommandResult updateSubmodulesResult = GitCommandResult.Success();
						if (submodulesToUpdate.Length > 0)
						{
							base.Dispatcher.Async(delegate
							{
								SetStatus(ForkPlusDialogStatus.InProgress, "Updating submodules...");
							});
							updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
							if (!updateSubmodulesResult.Succeeded)
							{
								base.Dispatcher.Async(delegate
								{
									Close(updateSubmodulesResult);
								});
								return;
							}
						}
						base.Dispatcher.Async(delegate
						{
							if (!applyStashResult.Succeeded)
							{
								Close(applyStashResult);
							}
							else if (!updateSubmodulesResult.Succeeded)
							{
								Close(updateSubmodulesResult);
							}
							else
							{
								Close(checkoutResult);
							}
						});
					}
				}
			}, JobFlags.SaveToLog);
		}

		private void CheckoutActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems[0] is CheckoutSyncOptionComboBoxItem { ActionType: { } actionType })
		{
			_actionType = actionType;
			RefreshTitle();
		}
		RefreshCommandPreview();
	}

		private void RefreshTitle()
		{
			switch (_actionType)
			{
			case CheckoutActionType.Rebase:
				base.SubmitButtonTitle = PreferencesLocalization.Current("Checkout and Rebase");
				break;
			case CheckoutActionType.Merge:
				base.SubmitButtonTitle = PreferencesLocalization.Current("Checkout and Merge");
				break;
			case CheckoutActionType.Reset:
				base.SubmitButtonTitle = PreferencesLocalization.Current("Checkout and Reset");
				break;
			default:
				base.SubmitButtonTitle = PreferencesLocalization.Current("Checkout");
				break;
			}
		}

		private static GitCommandResult PerformAction(GitModule gitModule, RemoteBranch remoteBranch, CheckoutActionType selectedActionType, RepositoryReferences references, JobMonitor monitor)
		{
			return selectedActionType switch
			{
				CheckoutActionType.Rebase => new RebaseBranchGitCommand().Execute(gitModule, remoteBranch.Sha.ToString(), rebaseMerges: false, updateRefs: false, monitor), 
				CheckoutActionType.Merge => new MergeGitCommand().Execute(gitModule, remoteBranch, MergeType.FastForward, references, monitor), 
				CheckoutActionType.Reset => new ResetCurrentBranchToRevisionGitCommand().Execute(gitModule, remoteBranch.Sha, BranchResetType.Hard, monitor), 
				_ => GitCommandResult.Success(), 
			};
		}

	}
}
