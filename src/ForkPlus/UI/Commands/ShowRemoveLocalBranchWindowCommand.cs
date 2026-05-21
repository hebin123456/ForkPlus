using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRemoveLocalBranchWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Delete Branch...", new Argument[1]
			{
				new Argument(ArgumentType.Branch, "branch to delete")
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (arguments[0] is Branch branch)
				{
					if (branch is RemoteBranch remoteBranch)
					{
						RepositoryUserControl.Commands.ShowRemoveRemoteBranchWindow.Execute(repositoryUserControl, new RemoteBranch[1] { remoteBranch });
					}
					else
					{
						RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.Execute(repositoryUserControl, new LocalBranch[1] { branch as LocalBranch });
					}
				}
			})
		};

		public string Title => "Delete Branch";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Delete);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch[] branches)
		{
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			RepositoryReferences references = repositoryData.References;
			if (references == null || branches.FirstItem() == null)
			{
				return;
			}
			Worktree? worktreeToRemove = null;
			if (branches.Length == 1 && repositoryData.Worktrees.WorktreesByFullReference.TryGetValue(branches[0].FullReference, out var value))
			{
				worktreeToRemove = value;
			}
			RemoveLocalBranchWindow removeLocalBranchWindow = new RemoveLocalBranchWindow(repositoryUserControl, references, branches, repositoryData.Remotes, worktreeToRemove);
			if (removeLocalBranchWindow.ShowDialog().GetValueOrDefault())
			{
				if (!removeLocalBranchWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, removeLocalBranchWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.Worktrees | SubDomain.References, new RevisionSelector.Sha(branches.FirstItem().Sha));
			}
		}
	}
}
