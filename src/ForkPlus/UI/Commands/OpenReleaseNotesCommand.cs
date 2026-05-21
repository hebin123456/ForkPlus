using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class OpenReleaseNotesCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
		}
	}
}
