namespace ForkPlus.Git.Commands
{
	public class CommitTemplate
	{
		public string Path { get; }

		public string StringValue { get; }

		public CommitTemplate(string path, string stringValue)
		{
			Path = path;
			StringValue = stringValue;
		}
	}
}
