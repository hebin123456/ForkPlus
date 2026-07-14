using System;
using System.Collections.Generic;
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
	public partial class RebaseBranchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly LocalBranch _source;

		private readonly IGitPoint _destination;

		private readonly bool _rebaseContainsLocalBranches;

		public RebaseBranchWindow(RepositoryUserControl repositoryUserControl, LocalBranch source, IGitPoint destination)
		{
			RebaseBranchWindow rebaseBranchWindow = this;
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Rebase");
			base.DialogDescription = PreferencesLocalization.Current("Copy commits from one branch to another");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Rebase");
			_repositoryUserControl = repositoryUserControl;
			_source = source;
			_destination = destination;
			SourceGitPointView.Value = source;
			DestinationGitPointView.Value = destination;
			AutostashCheckBox.IsChecked = ForkPlusSettings.Default.RebaseAutostash;
			UpdateRefsCheckBox.IsChecked = ForkPlusSettings.Default.RebaseUpdateRefs;
			GitCommandResult<RebaseTestGitCommand.TestResult> gitCommandResult = new RebaseTestGitCommand().Execute(repositoryUserControl.GitModule, source, destination.ObjectName);
			if (gitCommandResult.Succeeded)
			{
				if (gitCommandResult.Result == RebaseTestGitCommand.TestResult.Success)
				{
					SetStatus(ForkPlusDialogStatus.Success, "Rebase can be done without conflicts");
				}
				else if (gitCommandResult.Result == RebaseTestGitCommand.TestResult.Conflict)
				{
					SetStatus(ForkPlusDialogStatus.Warning, "Rebase will cause conflicts");
				}
			}
			else
			{
				Log.Warn(gitCommandResult.Error.FriendlyDescription);
			}
			Sha? sha = GetSha(destination);
			if (sha.HasValue)
			{
				Sha destinationSha = sha.GetValueOrDefault();
				GitCommandResult<Sha> mergeBaseResponse = new GetMergeBaseGitCommand().Execute(repositoryUserControl.GitModule, source.Sha, destinationSha);
				if (!mergeBaseResponse.Succeeded)
				{
					base.Dispatcher.Async(delegate
					{
						rebaseBranchWindow.Close(GitCommandResult.Failure(new GitCommandError.GenericError("Cannot get merge base: '" + source.Sha.ToString() + "..." + destinationSha.ToString() + "':\n" + mergeBaseResponse.Error.FriendlyDescription)));
					});
					return;
				}
				IReadOnlyList<LocalBranch> localBranchesInRange = GetLocalBranchesInRange(repositoryUserControl.RepositoryData.RevisionStorage, repositoryUserControl.RepositoryData.References, source.Sha, mergeBaseResponse.Result);
				_rebaseContainsLocalBranches = localBranchesInRange.Count > 0;
				if (_rebaseContainsLocalBranches)
				{
					UpdateRefsCheckBox.Visibility = Visibility.Visible;
					List<string> list = new List<string>(localBranchesInRange.Count);
					for (int i = 0; i < localBranchesInRange.Count; i++)
					{
						list.Add(localBranchesInRange[i].Name);
					}
					LocalBranchesListBox.ItemsSource = list;
				}
				else
				{
					UpdateRefsCheckBox.Visibility = Visibility.Collapsed;
				}
				RefreshBranchesListVisibility();
			}
			else
			{
				base.Dispatcher.Async(delegate
				{
					rebaseBranchWindow.Close(GitCommandResult.Failure(new GitCommandError.GenericError("Cannot get destination sha for rebase")));
				});
			}
			// InitializeComponent 期间 AddCommandPreview 已执行，但此时 _destination 及复选框状态尚未赋值，
			// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
			RefreshCommandPreview();
		}

		protected override string GetCommandPreview()
		{
			if (_destination == null)
			{
				return null;
			}
			var parts = new System.Collections.Generic.List<string> { "git", "rebase" };
			bool updateRefs = _rebaseContainsLocalBranches && UpdateRefsCheckBox.IsChecked.GetValueOrDefault();
			bool autostash = AutostashCheckBox.IsChecked.GetValueOrDefault();
			if (updateRefs)
			{
				parts.Add("--update-refs");
			}
			if (autostash)
			{
				parts.Add("--autostash");
			}
			parts.Add(_destination.ObjectName);
			return string.Join(" ", parts);
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
			LocalBranch source = _source;
			IGitPoint destination = _destination;
			bool workingDirectoryIsDirty = repositoryStatus.WorkingDirectoryIsDirty();
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			bool updateRefs = _rebaseContainsLocalBranches && UpdateRefsCheckBox.IsChecked.GetValueOrDefault();
			bool stashAndReapply = AutostashCheckBox.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.RebaseAutostash = stashAndReapply;
			ForkPlusSettings.Default.RebaseUpdateRefs = UpdateRefsCheckBox.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.Save();
			DisableEditableControls();
			_repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Rebase '{0}' onto '{1}'", source.Name, destination.FriendlyName), delegate(JobMonitor monitor)
			{
				if (stashAndReapply && workingDirectoryIsDirty)
				{
					monitor.Update(10.0, "Stashing...");
					GitCommandResult<bool> stashResult = new SaveStashGitCommand().Execute(gitModule, $"Rebase autostash {DateTime.Now}", stageNewFiles: false, monitor);
					if (!stashResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(GitCommandResult.Failure(stashResult.Error));
						});
						return;
					}
				}
				if (!source.IsActive)
				{
					base.Dispatcher.Async(delegate
					{
						SetStatus(ForkPlusDialogStatus.InProgress, "Checkout...");
					});
					GitCommandResult checkoutResult = new CheckoutBranchGitCommand().Execute(gitModule, source, monitor);
					if (!checkoutResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(checkoutResult);
						});
						return;
					}
				}
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Rebasing...");
				});
				GitCommandResult rebaseBranchResult = new RebaseBranchGitCommand().Execute(gitModule, destination.ObjectName, rebaseMerges: false, updateRefs, monitor);
				if (!rebaseBranchResult.Succeeded)
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
						Close(rebaseBranchResult);
					});
				}
				else
				{
					GitCommandResult applyStashResult = GitCommandResult.Success();
					if (stashAndReapply && workingDirectoryIsDirty)
					{
						monitor.Update(10.0, "Applying stash...");
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
							Close(rebaseBranchResult);
						}
					});
				}
			}, JobFlags.SaveToLog);
		}

		private static Sha? GetSha(IGitPoint gitPoint)
		{
			if (gitPoint is Revision revision)
			{
				return revision.Sha;
			}
			if (gitPoint is ForkPlus.Git.Reference reference)
			{
				return reference.Sha;
			}
			return null;
		}

		private static IReadOnlyList<LocalBranch> GetLocalBranchesInRange(RevisionStorage revisionStorage, RepositoryReferences references, Sha start, Sha end)
		{
			List<LocalBranch> list = new List<LocalBranch>();
			HashSet<Sha> hashSet = new HashSet<Sha>();
			hashSet.Add(start);
			HandleEnumerator enumerator = revisionStorage.GetEnumerator();
			while (enumerator.MoveNext())
			{
				RevisionStorage.Handle current = enumerator.Current;
				Sha sha = revisionStorage.GetSha(current);
				ShaBufferIterator parents = revisionStorage.GetParents(current);
				if (sha == end)
				{
					break;
				}
				if (!hashSet.Contains(sha))
				{
					continue;
				}
				if (references.ReferencesBySha.TryGetValue(sha, out var value))
				{
					int[] array = value;
					foreach (int num in array)
					{
						if (references.Items[num] is LocalBranch { IsActive: false } localBranch)
						{
							list.Add(localBranch);
						}
					}
				}
				hashSet.Remove(sha);
				ShaBufferIterator.Enumerator enumerator2 = parents.GetEnumerator();
				while (enumerator2.MoveNext())
				{
					Sha current2 = enumerator2.Current;
					hashSet.Add(current2);
				}
			}
			return list;
		}

		private void UpdateRefsCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshBranchesListVisibility();
			RefreshCommandPreview();
		}

		private void AutostashCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private void RefreshBranchesListVisibility()
		{
			if (_rebaseContainsLocalBranches && UpdateRefsCheckBox.IsChecked.GetValueOrDefault())
			{
				LocalBranchesListBox.Visibility = Visibility.Visible;
			}
			else
			{
				LocalBranchesListBox.Visibility = Visibility.Collapsed;
			}
		}

	}
}
