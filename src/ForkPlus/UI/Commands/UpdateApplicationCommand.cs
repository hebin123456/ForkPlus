using System.Threading.Tasks;
using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class UpdateApplicationCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Check for Updates...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute()
		{
			MainWindow.Instance?.CheckForUpdates();
		}

		public Task ExecuteAsync(bool silent)
		{
			Execute();
			return Task.CompletedTask;
		}
	}
}
