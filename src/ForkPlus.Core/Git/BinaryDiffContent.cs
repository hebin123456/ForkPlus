using System.IO;

namespace ForkPlus.Git
{
	public class BinaryDiffContent : DiffContent
	{
		[Null]
		public MemoryStream SrcData { get; }

		[Null]
		public MemoryStream DstData { get; }

		public BinaryDiffContent(ChangedFile changedFile, [Null] MemoryStream srcData, [Null] MemoryStream dstData)
			: base(changedFile)
		{
			SrcData = srcData;
			DstData = dstData;
		}
	}
}
