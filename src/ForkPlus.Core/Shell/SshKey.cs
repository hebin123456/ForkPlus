namespace ForkPlus.Shell
{
	public class SshKey
	{
		public string FilePath { get; }

		public string Title { get; }

		public string RawPublicKey { get; }

		public SshKey(string filePath, string title, string rawPublicKey)
		{
			FilePath = filePath;
			Title = title;
			RawPublicKey = rawPublicKey;
		}
	}
}
