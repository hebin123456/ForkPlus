namespace ForkPlus.Git.Diff.Presentation
{
	public class VisualChunk
	{
		public Range CharRange { get; }

		public Range[] CustomHunks { get; }

		public Range? HeaderCharRange { get; }

		public VisualLine[] VisualLines { get; }

		public Range LinesRange { get; }

		public Range InnerRange { get; }

		public VisualSubChunk[] VisualSubChunks { get; }

		public Chunk Node { get; }

		public VisualChunk(Range charRange, Range[] customHunks, Range? headerCharRange, VisualLine[] visualLines, Range linesRange, Range innerRange, VisualSubChunk[] visualSubChunks, Chunk node)
		{
			CharRange = charRange;
			CustomHunks = customHunks;
			VisualLines = visualLines;
			HeaderCharRange = headerCharRange;
			LinesRange = linesRange;
			InnerRange = innerRange;
			VisualSubChunks = visualSubChunks;
			Node = node;
		}
	}
}
