using System.Windows.Input;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowAboutWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "About " + App.AppName;


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new AboutWindow().ShowDialog();
		}
	}
}
