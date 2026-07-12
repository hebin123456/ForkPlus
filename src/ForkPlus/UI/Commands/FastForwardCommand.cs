using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class FastForwardCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Fast-Forward", new Argument[1]
			{
				new Argument(ArgumentType.LocalBranch)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				RepositoryUserControl.Commands.FastForward.Execute(repositoryUserControl, arguments[0] as LocalBranch);
			})
		};

		public string Title => "Fast-Forward";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch localBranch)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			string upstream = localBranch.UpstreamFullReference;
			if (upstream == null)
			{
				return;
			}
			RemoteBranch remoteBranch = IReadOnlyListExtensions.FirstItem(repositoryData.References.RemoteBranches, (RemoteBranch x) => x.FullReference == upstream);
			if (remoteBranch == null)
			{
				return;
			}
			SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
			repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Fast forward '{0}'", localBranch.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult fastForwardResult = PerformFastForward(gitModule, localBranch, submodulesToUpdate, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (localBranch.IsActive)
					{
						repositoryUserControl.Invalidate(SubDomain.Head);
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.References, new RevisionSelector.Sha(remoteBranch.Sha));
					if (!fastForwardResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, fastForwardResult.Error).ShowDialog();
					}
				});
			});
		}

		private GitCommandResult PerformFastForward(GitModule gitModule, LocalBranch localBranch, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			GitCommandResult gitCommandResult = new FastForwardGitCommand().Execute(gitModule, localBranch, monitor);
			GitCommandResult gitCommandResult2 = GitCommandResult.Success();
			if (localBranch.IsActive && submodulesToUpdate.Length > 0)
			{
				monitor.Update(0.0, "Updating submodules...");
				gitCommandResult2 = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
			}
			if (!gitCommandResult.Succeeded)
			{
				monitor.Fail(PreferencesLocalization.Current("Fast forward failed"));
				return gitCommandResult;
			}
			if (!gitCommandResult2.Succeeded)
			{
				monitor.Fail(PreferencesLocalization.Current("Update submodules failed"));
				return gitCommandResult2;
			}
			monitor.Success("Everything is up to date");
			return gitCommandResult;
		}
	}
}
