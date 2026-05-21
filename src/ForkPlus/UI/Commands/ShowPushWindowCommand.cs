using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowPushWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[2]
		{
			new CommandDescriptor("Push...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (repositoryUserControl.RepositoryData != null)
				{
					MainWindow.Commands.ShowPushWindow.Execute(repositoryUserControl);
				}
			}),
			new CommandDescriptor("Push", new Argument[1]
			{
				new Argument(ArgumentType.LocalBranch)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (repositoryUserControl.RepositoryData != null)
				{
					MainWindow.Commands.ShowPushWindow.Execute(repositoryUserControl, arguments[0] as LocalBranch);
				}
			})
		};

		public string Title => "Push...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch localBranch = null)
		{
			if (repositoryUserControl.RepositoryData != null)
			{
				new PushWindow(repositoryUserControl, null, localBranch).ShowDialog();
			}
		}
	}
}
