using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class ActivateCommitViewCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Show Uncommitted Changes";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.D1, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut { get; }

		public void Execute()
		{
			MainWindow.ActiveRepositoryUserControl?.ActivateCommitView();
		}
	}
}
