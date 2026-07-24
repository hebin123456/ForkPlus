using System.Threading.Tasks;
using Avalonia.Input;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class UpdateApplicationCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Check for Updates...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute()
		{
			ServiceLocator.WindowManager.CheckForUpdates();
		}

		public Task ExecuteAsync(bool silent)
		{
			Execute();
			return Task.CompletedTask;
		}
	}
}
