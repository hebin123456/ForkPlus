using System.IO;

namespace ForkPlus.Git
{
	public class ImageContent : BinaryContent
	{
		public MemoryStream Data { get; }

		public ImageContent(string path, bool isTracked, MemoryStream data)
			: base(path, isTracked, data?.Length)
		{
			Data = data;
		}
	}
}
