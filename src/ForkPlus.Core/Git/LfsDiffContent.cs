namespace ForkPlus.Git
{
	public class LfsDiffContent : DiffContent
	{
		public GitModule GitModule { get; }

		[Null]
		public LfsPointer Src { get; }

		[Null]
		public LfsPointer Dst { get; }

		public BinaryFileType BinaryFileType { get; }

		public LfsDiffContent(GitModule gitModule, ChangedFile changedFile, BinaryFileType binaryFileType, [Null] LfsPointer src, [Null] LfsPointer dst)
			: base(changedFile)
		{
			GitModule = gitModule;
			Src = src;
			Dst = dst;
			BinaryFileType = binaryFileType;
		}
	}
}
