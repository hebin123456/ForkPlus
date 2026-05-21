using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRepositoryStatisticsWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Repository Statistics...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryUserControl.Commands.ShowRepositoryStatisticsWindow.Execute(gitModule);
				}
			})
		};

		public string Title { get; } = "Repository Statistics...";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(GitModule gitModule)
		{
			new RepositoryStatisticsWindow(gitModule).ShowDialog();
		}
	}
}
