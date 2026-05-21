using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class ActivateSearchTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Activate Search Navigator";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.D2, ModifierKeys.Control | ModifierKeys.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			MainWindow.ActiveRepositoryUserControl?.SidebarActivateSearchTab();
		}
	}
}
