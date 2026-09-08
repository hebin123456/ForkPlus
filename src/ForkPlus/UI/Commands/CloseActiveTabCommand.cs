using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	public class CloseActiveTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Close Tab";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.W, global::Avalonia.Input.KeyModifiers.Control);


		public KeyGesture SecondaryShortcut { get; } = new KeyGesture(Key.F4, global::Avalonia.Input.KeyModifiers.Control);


		public void Execute()
		{
			((global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow).TabManager.CloseActiveTab();
		}
	}
}
