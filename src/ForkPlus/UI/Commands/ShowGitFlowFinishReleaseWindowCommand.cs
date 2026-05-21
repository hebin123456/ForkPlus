using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitFlowFinishReleaseWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git Flow: Finish Release...", new Argument[1]
			{
				new Argument(ArgumentType.ReleaseBranch)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryData repositoryData = repositoryUserControl.RepositoryData;
					if (repositoryData != null && arguments[0] is LocalBranch releaseBranch)
					{
						RepositoryUserControl.Commands.ShowGitFlowFinishReleaseWindow.Execute(repositoryUserControl, gitModule, repositoryData, releaseBranch);
					}
				}
			})
		};

		public string Title => "Finish Release...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, LocalBranch releaseBranch)
		{
			if (gitModule == null || repositoryData == null)
			{
				return;
			}
			GitFlowFinishReleaseWindow gitFlowFinishReleaseWindow = new GitFlowFinishReleaseWindow(gitModule, repositoryData, releaseBranch);
			if (gitFlowFinishReleaseWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.References, new RevisionSelector.Head());
				if (!gitFlowFinishReleaseWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitFlowFinishReleaseWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
