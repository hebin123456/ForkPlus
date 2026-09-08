using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Commands
{
	public class ExitApplicationCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Exit";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			global::ForkPlus.UI.WpfCompat.WpfApp.Shutdown(0);
		}
	}
}
