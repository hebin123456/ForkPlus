using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowSideBySideMergeWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Merge...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl, RepositoryState repositoryState, ChangedFile changedFile)
		{
			SideBySideMergeWindow sideBySideMergeWindow = new SideBySideMergeWindow(repositoryUserControl, repositoryState, changedFile);
			if (sideBySideMergeWindow.ShowDialog().GetValueOrDefault())
			{
				if (!sideBySideMergeWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, sideBySideMergeWindow.GitResult.Error).ShowDialog();
				}
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			}
		}
	}
}
