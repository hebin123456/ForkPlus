using System.Windows.Input;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowConfigureWorkspacesWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Configure...";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new ConfigureWorkspacesWindow().ShowDialog();
		}
	}
}
