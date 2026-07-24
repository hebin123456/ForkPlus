using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowSaveStashWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Save Stash...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					MainWindow.Commands.ShowSaveStashWindow.Execute(repositoryUserControl, gitModule);
				}
			})
		};

		public string Title => "Save Stash...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.H, KeyModifiers.Control | KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			if (repositoryUserControl.RepositoryData == null)
			{
				return;
			}
			SaveStashWindow saveStashWindow = new SaveStashWindow(gitModule);
			if (saveStashWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Stashes);
				if (!saveStashWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, saveStashWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
