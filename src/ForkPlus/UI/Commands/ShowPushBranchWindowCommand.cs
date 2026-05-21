using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowPushBranchWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Push...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] RepositoryUserControl repositoryUserControl, Remote remote = null, LocalBranch localBranch = null)
		{
			if (repositoryUserControl != null)
			{
				new PushWindow(repositoryUserControl, remote, localBranch).ShowDialog();
			}
		}
	}
}
