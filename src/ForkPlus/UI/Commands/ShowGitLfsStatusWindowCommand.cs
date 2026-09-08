using Avalonia.Input;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowGitLfsStatusWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git LFS: Status...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (repositoryUserControl.GitModule != null && repositoryUserControl.RepositoryData != null)
				{
					RepositoryUserControl.Commands.ShowGitLfsStatusWindow.Execute(repositoryUserControl);
				}
			})
		};

		public string Title => "Status...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			new GitLfsStatusWindow(repositoryUserControl).ShowDialog();
		}
	}
}
