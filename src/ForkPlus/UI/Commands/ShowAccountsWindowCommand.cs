using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.UI.Dialogs.Accounts;
using ForkPlus.UI.WpfCompat;

namespace ForkPlus.UI.Commands
{
	public class ShowAccountsWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Accounts...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			Dispatcher.UIThread.Post(delegate
			{
				AccountsWindow window = new AccountsWindow();
				Window owner = WpfApp.MainWindow;
				if (owner != null && owner.IsVisible)
				{
					window.SetOwnerAndCenter(owner);
					window.ShowDialog();
				}
				else
				{
					window.ShowDialog();
				}
			}, DispatcherPriority.Background);
		}
	}
}
