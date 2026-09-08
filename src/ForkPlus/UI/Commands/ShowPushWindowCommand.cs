using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowPushWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[2]
		{
			new CommandDescriptor("Push...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (repositoryUserControl.RepositoryData != null)
				{
					MainWindow.Commands.ShowPushWindow.Execute(repositoryUserControl);
				}
			}),
			new CommandDescriptor("Push", new Argument[1]
			{
				new Argument(ArgumentType.LocalBranch)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (repositoryUserControl.RepositoryData != null)
				{
					MainWindow.Commands.ShowPushWindow.Execute(repositoryUserControl, arguments[0] as LocalBranch);
				}
			})
		};

		public string Title => "Push...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.P, global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch localBranch = null)
		{
			if (repositoryUserControl.RepositoryData != null)
			{
				// v3.12.1：mm 子仓 push 防呆——检测 + 引导 + 逃生口（与 pull 防呆同构）
				if (!MmSubrepoPushGuard.ConfirmSingleRepoPush(repositoryUserControl))
				{
					return;
				}
				new PushWindow(repositoryUserControl, null, localBranch).ShowDialog();
			}
		}
	}
}
