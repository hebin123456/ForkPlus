using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowSaveSnapshotWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Save Snapshot...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			SaveSnapshotWindow saveSnapshotWindow = new SaveSnapshotWindow(repositoryUserControl);
			if (saveSnapshotWindow.ShowDialog().GetValueOrDefault())
			{
				repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Stashes);
				if (!saveSnapshotWindow.GitResult.Succeeded)
				{
					new ErrorWindow(repositoryUserControl, saveSnapshotWindow.GitResult.Error).ShowDialog();
				}
			}
		}
	}
}
