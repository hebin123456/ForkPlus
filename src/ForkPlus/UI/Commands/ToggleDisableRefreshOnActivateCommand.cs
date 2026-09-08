using Avalonia.Input;
using ForkPlus.Settings;

namespace ForkPlus.UI.Commands
{
	public class ToggleDisableRefreshOnActivateCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Disable Refresh on Activation";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			ForkPlusSettings.Default.DisableRefreshOnAppActivation = !ForkPlusSettings.Default.DisableRefreshOnAppActivation;
		}
	}
}
