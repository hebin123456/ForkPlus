namespace ForkPlus.Git
{
	public class LfsContent : BinaryContent
	{
		public LfsPointer LfsPointer { get; }

		public BinaryFileType BinaryFileType { get; }

		public LfsContent(string path, bool isTracked, LfsPointer lfsPointer, BinaryFileType binaryFileType)
			: base(path, isTracked, lfsPointer.Size)
		{
			LfsPointer = lfsPointer;
			BinaryFileType = binaryFileType;
		}
	}
}
