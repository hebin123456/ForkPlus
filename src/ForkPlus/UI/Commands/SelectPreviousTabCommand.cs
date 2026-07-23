using System.Windows.Input;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class SelectPreviousTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Select Previous Tab";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			ServiceLocator.WindowManager.SelectPreviousTab();
		}
	}
}
