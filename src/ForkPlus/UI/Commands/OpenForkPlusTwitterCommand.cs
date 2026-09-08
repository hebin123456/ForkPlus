using System;
using Avalonia.Input;

namespace ForkPlus.UI.Commands
{
	public class OpenForkPlusTwitterCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Open @git_fork on Twitter";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new Uri("https://twitter.com/git_fork").OpenInBrowser();
		}
	}
}
