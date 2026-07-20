namespace ForkPlus.Git
{
	public class BinaryContent : Content
	{
		public long? Size { get; }

		public BinaryContent(string path, bool isTracked, long? size)
			: base(path, isTracked)
		{
			Size = size;
		}
	}
}
