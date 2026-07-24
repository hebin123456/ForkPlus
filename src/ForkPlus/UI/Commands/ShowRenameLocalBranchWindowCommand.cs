using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRenameLocalBranchWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Rename Branch...", new Argument[1]
			{
				new Argument(ArgumentType.LocalBranch, "branch to rename")
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryData repositoryData = repositoryUserControl.RepositoryData;
					if (repositoryData != null && arguments[0] is LocalBranch localBranch)
					{
						RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.Execute(repositoryUserControl, gitModule, repositoryData.References, localBranch);
					}
				}
			})
		};

		public string Title { get; } = "Rename Branch...";


		public KeyGesture Shortcut { get; } = new KeyGesture(Key.F2);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryReferences references, LocalBranch localBranch, [Null] string newName = null)
		{
			if (localBranch == null)
			{
				return;
			}
			RenameLocalBranchWindow renameLocalBranchWindow = new RenameLocalBranchWindow(gitModule, references, localBranch, newName);
			if (renameLocalBranchWindow.ShowDialog().GetValueOrDefault())
			{
				if (!renameLocalBranchWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, renameLocalBranchWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References, new RevisionSelector.Sha(localBranch.Sha));
			}
		}
	}
}
