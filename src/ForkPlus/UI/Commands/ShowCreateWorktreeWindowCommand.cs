using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowCreateWorktreeWindowCommand : IUICommand, IForkPlusCommand, IPaletteCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Create Worktree...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				MainWindow.Commands.ShowCreateWorktreeWindow.Execute(repositoryUserControl);
			})
		};

		public string Title => "New Worktree...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			LocalBranch localBranch = repositoryData.References.LocalMain(gitModule) ?? repositoryData.References.LocalBranches.FirstItem();
			if (localBranch == null)
			{
				return;
			}
			CreateWorktreeWindow createWorktreeWindow = new CreateWorktreeWindow(repositoryUserControl, localBranch);
			if (createWorktreeWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Worktrees);
				if (!createWorktreeWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, createWorktreeWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
