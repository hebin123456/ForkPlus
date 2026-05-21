using System.Windows.Input;
using ForkPlus.Accounts;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowCloneWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Clone...";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.N, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] string url = null, [Null] Account account = null)
		{
			new CloneWindow(url, account).ShowDialog();
		}
	}
}
