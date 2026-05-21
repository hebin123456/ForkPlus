using System.IO;
using System.Windows.Input;
using ForkPlus.Git;

namespace ForkPlus.UI.Commands
{
	public class ShowFileInFileExplorerCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Show in File Explorer";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute([Null] GitModule gitModule, [Null] string filePath)
		{
			if (gitModule != null && filePath != null)
			{
				FileHelper.OpenInWindowsExplorer(Path.Combine(gitModule.Path, filePath).Replace("/", "\\"));
			}
		}
	}
}
