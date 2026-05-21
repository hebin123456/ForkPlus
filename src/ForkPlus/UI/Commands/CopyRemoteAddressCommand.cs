using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class CopyRemoteAddressCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Copy Remote Address";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] string remoteAddress)
		{
			if (remoteAddress != null)
			{
				ClipboardHelper.SetText(remoteAddress);
			}
		}
	}
}
