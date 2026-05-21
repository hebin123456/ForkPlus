using System.Windows.Input;
using ForkPlus.UI.QuickLaunch;

namespace ForkPlus.UI.Commands
{
	public class ShowQuickLaunchCheckoutWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Quick Launch...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.B, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new QuickLaunchWindow(showCheckout: true).Show();
		}
	}
}
