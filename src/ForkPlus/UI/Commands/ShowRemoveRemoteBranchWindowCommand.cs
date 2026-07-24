using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRemoveRemoteBranchWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Delete Branch";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Delete);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, RemoteBranch[] branches)
		{
			RepositoryReferences repositoryReferences = repositoryUserControl.RepositoryData?.References;
			if (repositoryReferences == null || branches.FirstItem() == null)
			{
				return;
			}
			RemoveRemoteBranchWindow removeRemoteBranchWindow = new RemoveRemoteBranchWindow(repositoryUserControl, branches, repositoryReferences);
			if (removeRemoteBranchWindow.ShowDialog().GetValueOrDefault())
			{
				if (!removeRemoteBranchWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, removeRemoteBranchWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References, new RevisionSelector.Sha(branches.FirstItem().Sha));
			}
		}
	}
}
