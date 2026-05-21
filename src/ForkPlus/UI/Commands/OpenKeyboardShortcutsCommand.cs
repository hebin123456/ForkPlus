using System.Windows.Input;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class OpenKeyboardShortcutsCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Keyboard Shortcuts";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new KeyboardShortcutsWindow().ShowDialog();
		}
	}
}
