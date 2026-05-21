using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitLfsFetchWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git LFS: Fetch...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null && repositoryUserControl.RepositoryData != null)
				{
					RepositoryUserControl.Commands.ShowGitLfsFetchWindow.Execute(repositoryUserControl, gitModule);
				}
			})
		};

		public string Title => "Fetch...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			if (repositoryUserControl.RepositoryData != null)
			{
				new GitLfsFetchWindow(repositoryUserControl, gitModule).ShowDialog();
			}
		}
	}
}
