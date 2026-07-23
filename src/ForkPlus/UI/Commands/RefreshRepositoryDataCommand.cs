using System.Windows;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class RefreshRepositoryDataCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Refresh";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.F5);


		public KeyGesture SecondaryShortcut { get; }

		public void Execute()
		{
			ServiceLocator.WindowManager.InvalidateAndRefreshActiveRepositoryView(SubDomain.All);
		}
	}
}
