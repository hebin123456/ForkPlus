using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class GitLfsPruneCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git LFS: Prune", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryUserControl.Commands.GitLfsPrune.Execute(repositoryUserControl, gitModule);
				}
			})
		};

		public string Title => "Prune";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.Current("LFS Prune"), delegate(JobMonitor monitor)
			{
				GitCommandResult pruneResult = new GitLfsPruneGitCommand().Execute(gitModule, monitor);
				repositoryUserControl.Dispatcher.Invoke(delegate
				{
					if (!pruneResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, pruneResult.Error).ShowDialog();
					}
				});
			});
		}
	}
}
