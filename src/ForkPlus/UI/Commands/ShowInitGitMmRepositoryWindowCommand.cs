using System.Windows.Input;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowInitGitMmRepositoryWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Initialize git mm Repository...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.G, ModifierKeys.Control);

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new InitGitMmRepositoryWindow().ShowDialog();
		}
	}
}
