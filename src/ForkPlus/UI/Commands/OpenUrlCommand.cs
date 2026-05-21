using System;
using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class OpenUrlCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(string url)
		{
			new Uri(url).OpenInBrowser();
		}
	}
}
