using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	public class SelectNextTabCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Select Next Tab";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.Tab, global::Avalonia.Input.KeyModifiers.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			((global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow).TabManager.SelectNextTab();
		}
	}
}
