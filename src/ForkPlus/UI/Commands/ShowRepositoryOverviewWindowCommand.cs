using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRepositoryOverviewWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Repository Treemap...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryUserControl.Commands.ShowRepositoryOverviewWindow.Execute(repositoryUserControl, gitModule);
				}
			})
		};

		public string Title { get; } = "Repository Treemap...";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			if (repositoryUserControl.RepositoryData != null)
			{
				new RepositoryOverviewWindow(repositoryUserControl, gitModule).Show();
			}
		}
	}
}
