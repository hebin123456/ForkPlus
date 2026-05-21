namespace ForkPlus.Accounts
{
	public class GitServiceRepository
	{
		public string Name { get; }

		public string Owner { get; }

		[Null]
		public string OwnerAvatarUrl { get; }

		public string GitHttpsUrl { get; }

		public string GitSshUrl { get; }

		public GitServiceRepository(string name, string owner, [Null] string ownerAvatarUrl, string gitHttpsUrl, string gitSshUrl)
		{
			Name = name;
			Owner = owner;
			OwnerAvatarUrl = ownerAvatarUrl;
			GitHttpsUrl = gitHttpsUrl;
			GitSshUrl = gitSshUrl;
		}
	}
}
