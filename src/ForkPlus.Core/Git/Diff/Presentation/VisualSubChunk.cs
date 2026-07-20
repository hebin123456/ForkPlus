namespace ForkPlus.Git.Diff.Presentation
{
	public class VisualSubChunk
	{
		public Range CharRange { get; }

		public Range PreContextLines { get; }

		public Range DeletedLines { get; }

		public Range AddedLines { get; }

		public Range PostContextLines { get; }

		public int[] PragmaLines { get; }

		public SubChunk Node { get; }

		public VisualSubChunk(Range charRange, Range preContextLines, Range deletedLines, Range addedLines, Range postContextLines, int[] pragmaLines, SubChunk node)
		{
			CharRange = charRange;
			PreContextLines = preContextLines;
			DeletedLines = deletedLines;
			AddedLines = addedLines;
			PostContextLines = postContextLines;
			PragmaLines = pragmaLines;
			Node = node;
		}
	}
}
