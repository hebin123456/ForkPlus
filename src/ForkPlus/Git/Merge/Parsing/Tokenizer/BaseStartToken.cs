namespace ForkPlus.Git.Merge.Parsing.Tokenizer
{
	public class BaseStartToken : MergeToken
	{
		public string Sha { get; }

		public BaseStartToken(Range range, string sha)
			: base(range)
		{
			Sha = sha;
		}
	}
}
