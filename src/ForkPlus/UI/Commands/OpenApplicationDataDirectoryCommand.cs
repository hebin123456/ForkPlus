using System.Diagnostics;
using System.Windows.Input;

namespace ForkPlus.UI.Commands
{
	public class OpenApplicationDataDirectoryCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Open Application Data Folder";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute()
		{
			Process process = new Process();
			ProcessStartInfo startInfo = new ProcessStartInfo(App.ForkDirectoryPath);
			process.StartInfo = startInfo;
			process.Start();
		}
	}
}
