using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitFlowStartHotfixWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git Flow: Start Hotfix...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryUserControl.Commands.ShowGitFlowStartHotfixWindow.Execute(repositoryUserControl, gitModule);
				}
			})
		};

		public string Title => "Start Hotfix...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			if (repositoryUserControl.RepositoryData == null)
			{
				return;
			}
			GitFlowStartHotfixWindow gitFlowStartHotfixWindow = new GitFlowStartHotfixWindow(gitModule);
			if (gitFlowStartHotfixWindow.ShowDialog().GetValueOrDefault())
			{
				if (!gitFlowStartHotfixWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitFlowStartHotfixWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.Head | SubDomain.References, new RevisionSelector.Head());
			}
		}
	}
}
