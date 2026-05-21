using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitFlowInitWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git Flow: Initialize...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryUserControl.Commands.ShowGitFlowInitWindow.Execute(repositoryUserControl, gitModule);
				}
			})
		};

		public string Title => "Initialize Git Flow...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			GitFlowInitWindow gitFlowInitWindow = new GitFlowInitWindow(gitModule);
			if (gitFlowInitWindow.ShowDialog().GetValueOrDefault())
			{
				if (!gitFlowInitWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, gitFlowInitWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.GitFlowSettings | SubDomain.References);
			}
		}
	}
}
