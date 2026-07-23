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
			// .NET 10 起 UseShellExecute 默认从 true 改为 false，文件夹路径不是可执行文件，
			// 必须显式置 true 才能走 Shell 打开资源管理器。
			Process process = new Process();
			ProcessStartInfo startInfo = new ProcessStartInfo(App.ForkDirectoryPath) { UseShellExecute = true };
			process.StartInfo = startInfo;
			process.Start();
		}
	}
}
