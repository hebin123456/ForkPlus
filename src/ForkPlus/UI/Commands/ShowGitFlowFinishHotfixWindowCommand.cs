using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitFlowFinishHotfixWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git Flow: Finish Hotfix...", new Argument[1]
			{
				new Argument(ArgumentType.HotfixBranch)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryData repositoryData = repositoryUserControl.RepositoryData;
					if (repositoryData != null && arguments[0] is LocalBranch hotfixBranch)
					{
						RepositoryUserControl.Commands.ShowGitFlowFinishHotfixWindow.Execute(repositoryUserControl, gitModule, repositoryData, hotfixBranch);
					}
				}
			})
		};

		public string Title => "Finish Hotfix...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData, LocalBranch hotfixBranch)
		{
			if (gitModule == null || repositoryData == null)
			{
				return;
			}
			GitFlowFinishHotfixWindow gitFlowFinishHotfixWindow = new GitFlowFinishHotfixWindow(gitModule, repositoryData, hotfixBranch);
			if (gitFlowFinishHotfixWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.References, new RevisionSelector.Head());
				if (!gitFlowFinishHotfixWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitFlowFinishHotfixWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
