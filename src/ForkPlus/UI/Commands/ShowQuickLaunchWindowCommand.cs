using Avalonia.Input;
using ForkPlus.UI.QuickLaunch;

namespace ForkPlus.UI.Commands
{
	public class ShowQuickLaunchWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Quick Launch...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.P, KeyModifiers.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new QuickLaunchWindow().Show();
		}
	}
}
