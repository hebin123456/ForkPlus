using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	public static class RevisionDetailsExtensions
	{
		public static bool IsStash(this RevisionDetails revisionDetails, RepositoryStashes stashes)
		{
			return stashes.Items.AnyItem((StashRevision x) => x.Sha == revisionDetails.Sha);
		}
	}
}
