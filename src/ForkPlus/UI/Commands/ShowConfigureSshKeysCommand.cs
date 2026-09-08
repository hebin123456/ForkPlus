using Avalonia.Input;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowConfigureSshKeysCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Configure SSH Keys...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new ConfigureSshKeysWindow().ShowDialog();
		}
	}
}
