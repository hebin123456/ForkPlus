using System.Windows.Input;
using ForkPlus.UI.Dialogs.Accounts;

namespace ForkPlus.UI.Commands
{
	public class ShowAccountsWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Accounts...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new AccountsWindow().ShowDialog();
		}
	}
}
