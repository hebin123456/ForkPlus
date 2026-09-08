using Avalonia.Input;

namespace ForkPlus.UI.Commands
{
	public class ActivateRepositoryTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Activate Repository Navigator";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.D1, global::Avalonia.Input.KeyModifiers.Control | global::Avalonia.Input.KeyModifiers.Shift);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			MainWindow.ActiveRepositoryUserControl?.SidebarActivateRepositoryTab();
		}
	}
}
