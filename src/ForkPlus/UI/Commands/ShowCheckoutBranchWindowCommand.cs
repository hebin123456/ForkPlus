using System.Windows.Input;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class ShowCheckoutBranchWindowCommand : IUICommand, IForkPlusCommand, IPaletteCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Checkout Branch", new Argument[1]
			{
				new Argument(ArgumentType.Branch)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				RepositoryUserControl.Commands.ShowCheckoutBranchWindow.Execute(repositoryUserControl, arguments[0] as Branch);
			})
		};

		public KeyGesture Shortcut => null;

		public string Title => "Checkout Branch";

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Branch branch)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData == null || repositoryUserControl.RepositoryStatus == null)
			{
				return;
			}
			CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			Worktree[] items = repositoryData.Worktrees.Items;
			LocalBranch localBranch = branch as LocalBranch;
			if (localBranch != null)
			{
				Worktree? worktree = items.FirstItemStruct((Worktree x) => x.HeadString == localBranch.FullReference);
				if (worktree.HasValue)
				{
					Worktree valueOrDefault = worktree.GetValueOrDefault();
					RepositoryUserControl.Commands.OpenWorktree.Execute(repositoryUserControl, gitModule, new Worktree[1] { valueOrDefault });
				}
				else
				{
					Checkout(repositoryUserControl, localBranch);
				}
			}
			else
			{
				if (!(branch is RemoteBranch remoteBranch))
				{
					return;
				}
				LocalBranch[] localBranches = repositoryData.References.LocalBranches;
				LocalBranch correspondingLocalBranch = localBranches.FirstItem(remoteBranch.FullReference, (LocalBranch x, string fullReference) => x.UpstreamFullReference == fullReference);
				if (correspondingLocalBranch == null)
				{
					TrackRemoteBranch(repositoryUserControl, localBranches, remoteBranch);
					return;
				}
				if (correspondingLocalBranch.Sha == remoteBranch.Sha)
				{
					if (!correspondingLocalBranch.IsActive)
					{
						Worktree? worktree = items.FirstItemStruct((Worktree x) => x.HeadString == correspondingLocalBranch.FullReference);
						if (worktree.HasValue)
						{
							Worktree valueOrDefault2 = worktree.GetValueOrDefault();
							RepositoryUserControl.Commands.OpenWorktree.Execute(repositoryUserControl, gitModule, new Worktree[1] { valueOrDefault2 });
						}
						else
						{
							Checkout(repositoryUserControl, correspondingLocalBranch);
						}
					}
					return;
				}
				GitCommandResult<BehindAheadCount> gitCommandResult = new GetBehindAheadCountGitCommand().Execute(gitModule, correspondingLocalBranch.Sha, remoteBranch.Sha, commitGraphCache);
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
					return;
				}
				bool num = gitCommandResult.Result.Left > 0;
				bool flag = gitCommandResult.Result.Right > 0;
				if (num && flag)
				{
					CheckoutAndSyncWithRemoteBranch(repositoryUserControl, correspondingLocalBranch, remoteBranch);
				}
				else if (!flag)
				{
					if (!correspondingLocalBranch.IsActive)
					{
						Checkout(repositoryUserControl, correspondingLocalBranch);
					}
				}
				else
				{
					Checkout(repositoryUserControl, correspondingLocalBranch, remoteBranch);
				}
			}
		}

		private static void TrackRemoteBranch(RepositoryUserControl repositoryUserControl, LocalBranch[] localBranches, RemoteBranch remoteBranch)
		{
			TrackRemoteBranchWindow trackRemoteBranchWindow = new TrackRemoteBranchWindow(repositoryUserControl, localBranches, remoteBranch);
			if (trackRemoteBranchWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Head | SubDomain.Stashes | SubDomain.Submodules | SubDomain.Worktrees | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Sha(remoteBranch.Sha));
				if (!trackRemoteBranchWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, trackRemoteBranchWindow.GitResult.Error).ShowDialog();
				}
			}
		}

		private static void Checkout(RepositoryUserControl repositoryUserControl, LocalBranch branch, RemoteBranch fastForwardTo = null)
		{
			RepositoryStatus repositoryStatus = repositoryUserControl.RepositoryStatus;
			if (repositoryStatus == null)
			{
				return;
			}
			if (!repositoryStatus.WorkingDirectoryIsDirty() && fastForwardTo == null)
			{
				QuickCheckout(repositoryUserControl, branch);
				return;
			}
			CheckoutBranchWindow checkoutBranchWindow = new CheckoutBranchWindow(repositoryUserControl, branch, fastForwardTo);
			if (checkoutBranchWindow.ShowDialog().GetValueOrDefault())
			{
				if (!checkoutBranchWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, checkoutBranchWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Head | SubDomain.Stashes | SubDomain.Submodules | SubDomain.Worktrees | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Sha(fastForwardTo?.Sha ?? branch.Sha));
			}
		}

		private static void CheckoutAndSyncWithRemoteBranch(RepositoryUserControl repositoryUserControl, LocalBranch branch, RemoteBranch remoteBranch)
		{
			CheckoutAndSyncWindow checkoutAndSyncWindow = new CheckoutAndSyncWindow(repositoryUserControl, branch, remoteBranch);
			if (checkoutAndSyncWindow.ShowDialog().GetValueOrDefault())
			{
				if (!checkoutAndSyncWindow.GitResult.Succeeded)
				{
					repositoryUserControl.Invalidate(SubDomain.Stashes);
					new ErrorWindow(repositoryUserControl, checkoutAndSyncWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.Stashes | SubDomain.Submodules | SubDomain.Worktrees | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Sha(branch.Sha));
			}
		}

		private static void QuickCheckout(RepositoryUserControl repositoryUserControl, LocalBranch branch)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
			repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.FormatCurrent("Checkout branch '{0}'", branch.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult result = PerformCheckout(gitModule, branch, submodulesToUpdate, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!monitor.IsCanceled && !result.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, result.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Head | SubDomain.Submodules | SubDomain.Worktrees | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Sha(branch.Sha));
				});
			});
		}

		private static GitCommandResult PerformCheckout(GitModule gitModule, LocalBranch branch, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			monitor.Update(0.0, branch.Name);
			GitCommandResult gitCommandResult = new CheckoutBranchGitCommand().Execute(gitModule, branch, monitor);
			if (!gitCommandResult.Succeeded && !monitor.IsCanceled)
			{
				return gitCommandResult;
			}
			if (submodulesToUpdate.Length > 0)
			{
				monitor.Update(0.0, "Updating submodules...");
				GitCommandResult gitCommandResult2 = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
				if (!gitCommandResult2.Succeeded)
				{
					monitor.Fail(ServiceLocator.Localization.Current("Updating submodules failed"));
					return gitCommandResult2;
				}
				monitor.Success("");
			}
			return gitCommandResult;
		}
	}
}
