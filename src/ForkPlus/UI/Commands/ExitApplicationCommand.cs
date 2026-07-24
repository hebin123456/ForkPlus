using Avalonia.Input;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class ExitApplicationCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Exit";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			ServiceLocator.AppContext.Shutdown();
		}
	}
}
