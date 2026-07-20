namespace ForkPlus.Git.Diff
{
	public class Chunk
	{
		public int FromStart { get; }

		public int FromLength { get; }

		public int ToStart { get; }

		public int ToLength { get; }

		[Null]
		public string ContextString { get; }

		public SubChunk[] SubChunks { get; }

		public Chunk(int fromStart, int fromLength, int toStart, int toLength, [Null] string contextString, SubChunk[] subChunks)
		{
			FromStart = fromStart;
			FromLength = fromLength;
			ToStart = toStart;
			ToLength = toLength;
			ContextString = contextString;
			SubChunks = subChunks;
		}
	}
}
