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

		/// <param name="path">v4.5.0：仓库内路径；给出后，插件认领的格式也能得到位图，
		/// 使 Swipe / 洋葱皮 / 像素高亮对插件视图同样可用。未知时可为 null（仅内置解码）。</param>
		public static ImageData Create(MemoryStream memoryStream, bool isLfs, bool isTracked, [Null] string path = null)
		{
			// v4.5.0：解码改经 BinaryContentRenderer——内置走原路径，插件格式转交插件进程。
			return new ImageData(BinaryContentRenderer.CreateStatic(path, memoryStream, out _), memoryStream.Length, isLfs, isTracked, AnimatedImage.IsAnimatedStream(memoryStream));
		}

		public static ImageData Create(ImageContent imageContent)
		{
			return new ImageData(BinaryContentRenderer.CreateStatic(imageContent.Path, imageContent.Data, out _), imageContent.Size.GetValueOrDefault(), isLfs: false, imageContent.IsTracked, AnimatedImage.IsAnimatedStream(imageContent.Data));
		}
	}
}
