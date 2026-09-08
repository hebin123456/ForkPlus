using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowPullWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Pull...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				MainWindow.Commands.ShowPullWindow.Execute(repositoryUserControl);
			})
		};

		public string Title => "Pull...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.L, global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, [Null] RemoteBranch remoteBranch = null)
		{
			if (repositoryUserControl.RepositoryData != null)
			{
				// v3.11.2：mm 子仓 pull 防呆——检测 + 引导 + 逃生口
				if (!MmSubrepoPullGuard.ConfirmSingleRepoPull(repositoryUserControl))
				{
					return;
				}
				new PullWindow(repositoryUserControl, remoteBranch).ShowDialog();
			}
		}
	}
}
