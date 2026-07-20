namespace ForkPlus.Git
{
	public class UnknownBinaryDiffContent : DiffContent
	{
		public long? SrcSize { get; }

		public long? DstSize { get; }

		public UnknownBinaryDiffContent(ChangedFile changedFile, long? srcSize, long? dstSize)
			: base(changedFile)
		{
			SrcSize = srcSize;
			DstSize = dstSize;
		}
	}
}
