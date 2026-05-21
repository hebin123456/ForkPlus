using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowRevertRevisionWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Revert Commit...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, DecoratedRevision revision)
		{
			if (revision == null)
			{
				return;
			}
			Sha[] revisionParents = revision.GetParents().ToArray();
			RevertRevisionWindow revertRevisionWindow = new RevertRevisionWindow(repositoryUserControl, revision.ToRevision(), revisionParents);
			if (revertRevisionWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.References, new RevisionSelector.Sha(revision.Sha));
				if (!revertRevisionWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, revertRevisionWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
