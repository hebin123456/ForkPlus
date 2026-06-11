using System.Windows.Input;
using ForkPlus.Services;

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
				ServiceLocator.Clipboard.SetText(remoteAddress);
			}
		}
	}
}
