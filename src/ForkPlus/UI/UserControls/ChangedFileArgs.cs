using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	internal class ChangedFileArgs
	{
		public ChangedFile ChangedFile { get; }

		public bool LoadLargeUntrackedFiles { get; }

		public ChangedFileArgs(ChangedFile changedFile, bool loadLargeUntrackedFiles)
		{
			ChangedFile = changedFile;
			LoadLargeUntrackedFiles = loadLargeUntrackedFiles;
		}
	}
}
