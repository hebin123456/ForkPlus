using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRepositorySettingsWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Repository Settings...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryData repositoryData = repositoryUserControl.RepositoryData;
					if (repositoryData != null)
					{
						RepositoryUserControl.Commands.ShowRepositorySettingsWindow.Execute(gitModule, repositoryData);
					}
				}
			})
		};

		public string Title { get; } = "Settings for This Repository...";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(GitModule gitModule, RepositoryData repositoryData)
		{
			new RepositorySettingsWindow(gitModule, repositoryData).ShowDialog();
			ServiceLocator.WindowManager.InvalidateAndRefreshActiveRepositoryView(SubDomain.All);
		}
	}
}
