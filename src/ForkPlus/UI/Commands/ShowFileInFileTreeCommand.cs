using System.Windows.Input;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowFileInFileTreeCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Show in File Tree";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RevisionDetailsUserControl revisionDetailsUserControl, string filePath)
		{
			revisionDetailsUserControl.ShowInFileTreeTab(filePath);
		}
	}
}
