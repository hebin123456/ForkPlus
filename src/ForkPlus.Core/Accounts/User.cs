namespace ForkPlus.Accounts
{
	public class User
	{
		public string Username { get; }

		[Null]
		public string DisplayName { get; }

		[Null]
		public string AvatarUrl { get; }

		[Null]
		public string ProfileUrl { get; }

		public User(string username, [Null] string displayName, [Null] string avatarUrl, [Null] string profileUrl)
		{
			Username = username;
			DisplayName = displayName;
			AvatarUrl = avatarUrl;
			ProfileUrl = profileUrl;
		}
	}
}
