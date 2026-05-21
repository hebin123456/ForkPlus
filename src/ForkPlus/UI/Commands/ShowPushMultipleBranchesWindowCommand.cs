using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowPushMultipleBranchesWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Push...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch[] localBranches, Remote remote)
		{
			new PushMultipleBranchesWindow(repositoryUserControl, localBranches, remote).ShowDialog();
		}
	}
}
