using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitLfsPullWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git LFS: Pull...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null && repositoryUserControl.RepositoryData != null)
				{
					RepositoryUserControl.Commands.ShowGitLfsPullWindow.Execute(repositoryUserControl, gitModule);
				}
			})
		};

		public string Title => "Pull...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			if (repositoryUserControl.RepositoryData != null)
			{
				new GitLfsPullWindow(repositoryUserControl, gitModule).ShowDialog();
			}
		}
	}
}
