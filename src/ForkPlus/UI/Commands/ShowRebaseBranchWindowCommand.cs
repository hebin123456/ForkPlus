using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRebaseBranchWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Rebase...", new Argument[1]
			{
				new Argument(ArgumentType.Reference)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (repositoryUserControl.GitModule != null)
				{
					RepositoryData repositoryData = repositoryUserControl.RepositoryData;
					if (repositoryData != null)
					{
						LocalBranch activeBranch = repositoryData.References.ActiveBranch;
						RepositoryUserControl.Commands.ShowRebaseBranchWindow.Execute(repositoryUserControl, activeBranch, arguments[0] as Reference);
					}
				}
			})
		};

		public string Title => "Rebase Branch";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch source, IGitPoint destination)
		{
			if (repositoryUserControl.GitModule == null || source == null || destination == null)
			{
				return;
			}
			RebaseBranchWindow rebaseBranchWindow = new RebaseBranchWindow(repositoryUserControl, source, destination);
			if (rebaseBranchWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.Stashes | SubDomain.Submodules | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Head());
				if (!rebaseBranchWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, rebaseBranchWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
