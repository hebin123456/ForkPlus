using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class RemoveReferenceCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Delete);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, Reference[] referencesToRemove)
		{
			Reference reference = referencesToRemove.SingleItem();
			if (reference != null)
			{
				if (reference is LocalBranch { IsActive: false } localBranch2)
				{
					RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.Execute(repositoryUserControl, new LocalBranch[1] { localBranch2 });
				}
				else if (reference is RemoteBranch remoteBranch)
				{
					RepositoryUserControl.Commands.ShowRemoveRemoteBranchWindow.Execute(repositoryUserControl, new RemoteBranch[1] { remoteBranch });
				}
				else if (reference is Tag tag)
				{
					RepositoryUserControl.Commands.ShowRemoveTagWindow.Execute(repositoryUserControl, new Tag[1] { tag });
				}
			}
			else if (referencesToRemove.Length == 2)
			{
				Reference reference2 = IReadOnlyListExtensions.FirstItem(referencesToRemove, (Reference x) => x is LocalBranch);
				LocalBranch localBranch = reference2 as LocalBranch;
				if (localBranch != null && !localBranch.IsActive && referencesToRemove.ContainsItem((Reference x) => (x as RemoteBranch)?.FullReference == localBranch.UpstreamFullReference))
				{
					RepositoryUserControl.Commands.ShowRemoveLocalBranchWindow.Execute(repositoryUserControl, new LocalBranch[1] { localBranch });
				}
			}
		}
	}
}
