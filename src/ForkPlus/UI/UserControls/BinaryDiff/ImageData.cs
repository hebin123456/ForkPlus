using System.IO;
using Avalonia.Media.Imaging;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public class ImageData
	{
		[Null]
		public global::Avalonia.Media.Imaging.Bitmap ImageSource { get; }

		public long FileSize { get; }

		public bool IsLfs { get; }

		public bool IsTracked { get; }

		/// <summary>v4.3.2：该图片是否为动图（GIF / 动态 WebP / APNG）。动图禁用像素差异高亮。</summary>
		public bool IsAnimated { get; }

		public ImageData([Null] global::Avalonia.Media.Imaging.Bitmap imageSource, long fileSize, bool isLfs, bool isTracked, bool isAnimated = false)
		{
			ImageSource = imageSource; // Migration note：转换器误把属性名 ImageSource 写成类型全限定名。
			FileSize = fileSize;
			IsLfs = isLfs;
			IsTracked = isTracked;
			IsAnimated = isAnimated;
		}

		public static ImageData Create(MemoryStream memoryStream, bool isLfs, bool isTracked)
		{
			return new ImageData(BinaryDiffUserControl.CreateBitmapSource(memoryStream), memoryStream.Length, isLfs, isTracked, AnimatedImage.IsAnimatedStream(memoryStream));
		}

		public static ImageData Create(ImageContent imageContent)
		{
			return new ImageData(BinaryDiffUserControl.CreateBitmapSource(imageContent.Data), imageContent.Size.GetValueOrDefault(), isLfs: false, imageContent.IsTracked, AnimatedImage.IsAnimatedStream(imageContent.Data));
		}
	}
}
