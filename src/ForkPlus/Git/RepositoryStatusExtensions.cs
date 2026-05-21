namespace ForkPlus.Git
{
	public static class RepositoryStatusExtensions
	{
		public static bool WorkingDirectoryIsDirty(this RepositoryStatus repositoryStatus)
		{
			return repositoryStatus.ChangedFiles.AnyItem((ChangedFile x) => x.Tracked && !(x is SubmoduleChangedFile));
		}
	}
}
