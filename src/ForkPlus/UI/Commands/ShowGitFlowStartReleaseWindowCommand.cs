using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitFlowStartReleaseWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git Flow: Start Release...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryUserControl.Commands.ShowGitFlowStartReleaseWindow.Execute(repositoryUserControl, gitModule);
				}
			})
		};

		public string Title => "Start Release...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			if (gitModule == null)
			{
				return;
			}
			GitFlowStartReleaseWindow gitFlowStartReleaseWindow = new GitFlowStartReleaseWindow(gitModule);
			if (gitFlowStartReleaseWindow.ShowDialog().GetValueOrDefault())
			{
				if (!gitFlowStartReleaseWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitFlowStartReleaseWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.Head | SubDomain.References, new RevisionSelector.Head());
			}
		}
	}
}
