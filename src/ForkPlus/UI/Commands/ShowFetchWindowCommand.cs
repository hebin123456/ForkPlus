using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowFetchWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[2]
		{
			new CommandDescriptor("Fetch...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule2 = repositoryUserControl.GitModule;
				if (gitModule2 != null && repositoryUserControl.RepositoryData != null)
				{
					MainWindow.Commands.ShowFetchWindow.Execute(repositoryUserControl, gitModule2);
				}
			}),
			new CommandDescriptor("Fetch", new Argument[1]
			{
				new Argument(ArgumentType.Remote)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null && repositoryUserControl.RepositoryData != null)
				{
					MainWindow.Commands.ShowFetchWindow.Execute(repositoryUserControl, gitModule, arguments[0] as Remote);
				}
			})
		};

		public string Title => "Fetch...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remote = null)
		{
			if (repositoryUserControl.RepositoryData != null)
			{
				new FetchWindow(repositoryUserControl, gitModule, remote).ShowDialog();
			}
		}
	}
}
