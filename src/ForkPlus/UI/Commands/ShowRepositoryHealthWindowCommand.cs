using Avalonia.Input;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	// WS4 仓库健康仪表盘入口命令：Repository 菜单项 + QuickLaunch 均由此提供。
	// 窗口需要 RepositoryUserControl（JobQueue / RepositoryData / AddUndoable），故参数类型与
	// ShowBenchmarkWindowCommand 一致；命令注册在 MainWindowCommands（仿 ShowBenchmarkWindowCommand）。
	public class ShowRepositoryHealthWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Repository Health...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				MainWindow.Commands.ShowRepositoryHealthWindow.Execute(repositoryUserControl);
			})
		};

		public string Title => "Repository Health...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			new RepositoryHealthWindow(repositoryUserControl).ShowDialog();
		}
	}
}
