using System.IO;
using System.Windows.Media.Imaging;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public class ImageData
	{
		[Null]
		public BitmapSource ImageSource { get; }

		public long FileSize { get; }

		public bool IsLfs { get; }

		public bool IsTracked { get; }

		public ImageData([Null] BitmapSource imageSource, long fileSize, bool isLfs, bool isTracked)
		{
			ImageSource = imageSource;
			FileSize = fileSize;
			IsLfs = isLfs;
			IsTracked = isTracked;
		}

		public static ImageData Create(MemoryStream memoryStream, bool isLfs, bool isTracked)
		{
			return new ImageData(BinaryDiffUserControl.CreateBitmapSource(memoryStream), memoryStream.Length, isLfs, isTracked);
		}

		public static ImageData Create(ImageContent imageContent)
		{
			return new ImageData(BinaryDiffUserControl.CreateBitmapSource(imageContent.Data), imageContent.Size.GetValueOrDefault(), isLfs: false, imageContent.IsTracked);
		}
	}
}
