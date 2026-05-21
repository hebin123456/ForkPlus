using System.Windows;
using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class CloseActiveTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Close Tab";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.W, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut { get; } = new KeyGesture(Key.F4, ModifierKeys.Control);


		public void Execute()
		{
			(Application.Current.MainWindow as MainWindow).TabManager.CloseActiveTab();
		}
	}
}
