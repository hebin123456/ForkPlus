using Avalonia.Input;
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
				// v3.12.1：mm 子仓 push 防呆——检测 + 引导 + 逃生口（与 pull 防呆同构）
				if (!MmSubrepoPushGuard.ConfirmSingleRepoPush(repositoryUserControl))
				{
					return;
				}
				new PushWindow(repositoryUserControl, remote, localBranch).ShowDialog();
			}
		}
	}
}
