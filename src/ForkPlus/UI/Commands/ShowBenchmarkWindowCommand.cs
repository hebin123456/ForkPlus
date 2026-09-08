using Avalonia.Input;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowBenchmarkWindowCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Benchmark...", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				MainWindow.Commands.ShowBenchmarkWindow.Execute(repositoryUserControl);
			})
		};

		public string Title => "Performance Benchmark...";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl)
		{
			new BenchmarkWindow(repositoryUserControl).ShowDialog();
		}
	}
}
