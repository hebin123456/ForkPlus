using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	public class NewTabCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Repository Manager", new Argument[0], delegate
			{
				MainWindow.Commands.NewTab.Execute();
			})
		};

		public string Title => "New Tab";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.T, global::Avalonia.Input.KeyModifiers.Control);


		public KeyGesture SecondaryShortcut { get; }

		public void Execute()
		{
			((global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow).TabManager.NewTab();
		}
	}
}
