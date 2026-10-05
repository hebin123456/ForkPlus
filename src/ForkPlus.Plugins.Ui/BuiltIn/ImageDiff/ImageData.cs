using System;
using System.IO;
using Avalonia.Media.Imaging;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
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

		/// <summary>从字节流构造（经 CreateBitmapSource 解码 + 动图容器头判定）。解码失败返回 null。</summary>
		public static ImageData Create(MemoryStream memoryStream, bool isLfs, bool isTracked)
		{
			return new ImageData(CreateBitmapSource(memoryStream), memoryStream.Length, isLfs, isTracked, AnimatedImage.IsAnimatedStream(memoryStream));
		}

		/// <summary>
		/// 从主工程 BinaryDiffUserControl.CreateBitmapSource 迁移（v5.0.0 插件化）：
		/// WPF BitmapImage{CacheOption=OnLoad} + FormatConvertedBitmap(Pbgra32) 的 Avalonia 等价实现——
		/// 直接 Bitmap(Stream) 同步解码，解码结果即为统一的 Bgra8888，无需格式转换。解码失败返回 null。
		/// </summary>
		public static global::Avalonia.Media.Imaging.Bitmap CreateBitmapSource(MemoryStream stream)
		{
			try
			{
				stream.Position = 0L;
				return new global::Avalonia.Media.Imaging.Bitmap(stream);
			}
			catch (Exception ex)
			{
				PluginLog.Error("Failed to create BitmapSource", ex);
				return null;
			}
		}
	}
}
