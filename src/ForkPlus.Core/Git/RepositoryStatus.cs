namespace ForkPlus.Git
{
	public class RepositoryStatus
	{
		public RepositoryState RepositoryState { get; }

		public int FilesCount { get; }

		public ChangedFile[] ChangedFiles { get; }

		public RepositoryStatus(RepositoryState repositoryState, int filesCount, ChangedFile[] changedFiles)
		{
			RepositoryState = repositoryState;
			FilesCount = filesCount;
			ChangedFiles = changedFiles;
		}
	}
}
