using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;

namespace ForkPlus.UI.Commands
{
	public class RefreshRepositoryDataCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Refresh";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.F5);


		public KeyGesture SecondaryShortcut { get; }

		public void Execute()
		{
			Application.Current.ActiveRepositoryUserControl()?.InvalidateAndRefresh(SubDomain.All);
		}
	}
}
