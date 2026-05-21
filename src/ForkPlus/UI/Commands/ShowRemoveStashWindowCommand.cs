using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRemoveStashWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Delete);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, StashRevision[] stashes)
		{
			if (stashes.Length == 0)
			{
				return;
			}
			Sha? sha = ParentToSelectAfterwards(repositoryUserControl.RepositoryData.Stashes, stashes);
			RemoveStashWindow removeStashWindow = new RemoveStashWindow(repositoryUserControl, stashes);
			if (removeStashWindow.ShowDialog().GetValueOrDefault())
			{
				if (!removeStashWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, removeStashWindow.GitResult.Error).ShowDialog();
				}
				if (sha.HasValue)
				{
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.Stashes, new RevisionSelector.Sha(sha.Value));
				}
			}
		}

		private Sha? ParentToSelectAfterwards(RepositoryStashes repositoryStashes, StashRevision[] stashedToRemove)
		{
			StashRevision stashRevision = stashedToRemove.FirstItem();
			if (stashRevision == null)
			{
				return null;
			}
			HandleEnumerator enumerator = repositoryStashes.GetEnumerator();
			while (enumerator.MoveNext())
			{
				RevisionStorage.Handle current = enumerator.Current;
				if (repositoryStashes.GetSha(current) == stashRevision.Sha)
				{
					ShaBufferIterator.Enumerator enumerator2 = repositoryStashes.GetParents(current).GetEnumerator();
					if (enumerator2.MoveNext())
					{
						return enumerator2.Current;
					}
				}
			}
			return null;
		}
	}
}
