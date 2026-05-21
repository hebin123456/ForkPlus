namespace ForkPlus.Git.Merge.Parsing.Tokenizer
{
	public class ConflictEndToken : MergeToken
	{
		public string RemoteName { get; }

		public ConflictEndToken(Range range, string remoteName)
			: base(range)
		{
			RemoteName = remoteName;
		}
	}
}
