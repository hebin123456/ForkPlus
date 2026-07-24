// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media.Imaging → using Bitmap = Avalonia.Media.Imaging.Bitmap（别名替代 BitmapSource）
// - BitmapSource → Bitmap（Avalonia.Media.Imaging.Bitmap，参考 IconTools）
// - 新增 RawBytes 属性：存储原始字节，供 BinaryDiffUserControl.GetDiffImage 用 System.Drawing 做像素级比较
//   （Avalonia 不可变 Bitmap 不直接暴露像素数据，需保留原始字节供 System.Drawing.Bitmap 解码访问）
using System.IO;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public class ImageData
	{
		[Null]
		public Bitmap ImageSource { get; }

		[Null]
		public byte[] RawBytes { get; }

		public long FileSize { get; }

		public bool IsLfs { get; }

		public bool IsTracked { get; }

		public ImageData([Null] Bitmap imageSource, long fileSize, bool isLfs, bool isTracked, [Null] byte[] rawBytes = null)
		{
			ImageSource = imageSource;
			RawBytes = rawBytes;
			FileSize = fileSize;
			IsLfs = isLfs;
			IsTracked = isTracked;
		}

		public static ImageData Create(MemoryStream memoryStream, bool isLfs, bool isTracked)
		{
			byte[] rawBytes = memoryStream?.ToArray();
			return new ImageData(BinaryDiffUserControl.CreateBitmapSource(memoryStream), memoryStream.Length, isLfs, isTracked, rawBytes);
		}

		public static ImageData Create(ImageContent imageContent)
		{
			byte[] rawBytes = imageContent.Data?.ToArray();
			return new ImageData(BinaryDiffUserControl.CreateBitmapSource(imageContent.Data), imageContent.Size.GetValueOrDefault(), isLfs: false, imageContent.IsTracked, rawBytes);
		}
	}
}
