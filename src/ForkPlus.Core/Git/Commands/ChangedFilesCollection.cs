namespace ForkPlus.Git.Commands
{
	public struct ChangedFilesCollection
	{
		public int FilesCount { get; }

		public ChangedFile[] ChangedFiles { get; }

		public ChangedFilesCollection(int filesCount, ChangedFile[] changedFiles)
		{
			FilesCount = filesCount;
			ChangedFiles = changedFiles;
		}
	}
}
