namespace ForkPlus.Git
{
	public static class RepositoryRemotesExtensions
	{
		public static bool HasLfsCompatibleRemotes(this RepositoryRemotes remotes)
		{
			return remotes.Items.ContainsItem((Remote x) => x.RemoteType != RemoteType.Bitbucket);
		}
	}
}
