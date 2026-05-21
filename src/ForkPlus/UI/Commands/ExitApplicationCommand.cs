using System.Windows;
using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class ExitApplicationCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Exit";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			Application.Current.Shutdown(0);
		}
	}
}
