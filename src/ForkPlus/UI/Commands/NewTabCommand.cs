using Avalonia.Input;
using ForkPlus.Services;

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

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.T, KeyModifiers.Control);


		public KeyGesture SecondaryShortcut { get; }

		public void Execute()
		{
			ServiceLocator.WindowManager.NewTab();
		}
	}
}
