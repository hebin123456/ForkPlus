namespace ForkPlus.Git.Merge.Parsing.Tokenizer
{
	public class ConflictStartToken : MergeToken
	{
		public string LocalName { get; }

		public ConflictStartToken(Range range, string localName)
			: base(range)
		{
			LocalName = localName;
		}
	}
}
