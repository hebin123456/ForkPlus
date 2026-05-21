using System;
using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class OpenIssueTrackerCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "Issue Tracker";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			new Uri("https://github.com/ForkIssues/TrackerWin").OpenInBrowser();
		}
	}
}
