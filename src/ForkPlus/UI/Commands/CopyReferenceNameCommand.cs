using System.Windows.Input;
using ForkPlus.Git;

namespace ForkPlus.UI.Commands
{
	public class CopyReferenceNameCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(Reference reference)
		{
			ClipboardHelper.SetText(reference.Name);
		}
	}
}
