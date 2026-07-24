using Avalonia.Input;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class SelectNextTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Select Next Tab";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Tab, KeyModifiers.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			ServiceLocator.WindowManager.SelectNextTab();
		}
	}
}
