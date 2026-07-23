using System.Windows.Input;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class SelectNextTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Select Next Tab";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Tab, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			ServiceLocator.WindowManager.SelectNextTab();
		}
	}
}
