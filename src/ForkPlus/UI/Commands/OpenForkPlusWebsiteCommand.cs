using System;
using Avalonia.Input;

namespace ForkPlus.UI.Commands
{
	public class OpenForkPlusWebsiteCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Visit Fork Website";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new Uri("https://hebin.me").OpenInBrowser();
		}
	}
}
