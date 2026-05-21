using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class ActivateRepositoryTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Activate Repository Navigator";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.D1, ModifierKeys.Control | ModifierKeys.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			MainWindow.ActiveRepositoryUserControl?.SidebarActivateRepositoryTab();
		}
	}
}
