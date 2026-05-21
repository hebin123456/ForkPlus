namespace ForkPlus.Git.Merge.Parsing.Tokenizer
{
	public abstract class MergeToken
	{
		public Range Range { get; }

		public MergeToken(Range range)
		{
			Range = range;
		}
	}
}
