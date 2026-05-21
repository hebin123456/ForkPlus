using System.Windows;
using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class SelectNextTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Select Next Tab";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Tab, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			(Application.Current.MainWindow as MainWindow).TabManager.SelectNextTab();
		}
	}
}
