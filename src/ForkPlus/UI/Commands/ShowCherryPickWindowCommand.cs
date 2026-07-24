using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowCherryPickWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Cherry-pick Commit...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, DecoratedRevision[] revisions)
		{
			Sha[] firstRevisionParents = revisions.FirstItem().GetParents().ToArray();
			CherryPickWindow cherryPickWindow = new CherryPickWindow(repositoryUserControl, revisions.Map((DecoratedRevision x) => x.ToRevision()), firstRevisionParents);
			if (cherryPickWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.References, new RevisionSelector.Sha(revisions.LastItem().Sha));
				if (!cherryPickWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, cherryPickWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
