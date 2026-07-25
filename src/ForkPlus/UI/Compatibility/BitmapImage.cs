// 阶段 5：Avalonia 11.3 无 BitmapImage 类型（WPF System.Windows.Media.Imaging.BitmapImage）。
// Avalonia 使用 Bitmap（从 Stream/文件路径加载），但 Bitmap 无参数化构造函数，XAML 无法直接实例化。
//
// 本桥接类实现 IImage 接口，提供 UriSource 属性（string），在 XAML 中可像 WPF BitmapImage 一样使用：
//   <BitmapImage x:Key="MyIcon" UriSource="/Assets/icon.png" />
// 通过 XmlnsDefinition 映射到默认 XAML 命名空间，XAML 无需额外 xmlns 声明。
using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Avalonia.Media.Imaging
{
	/// <summary>
	/// WPF BitmapImage 的 Avalonia 兼容桥接。实现 IImage 接口，
	/// 可用于 Image.Source、ResourceDictionary 等 XAML 场景。
	/// </summary>
	public class BitmapImage : AvaloniaObject, IImage
	{
		private Bitmap _bitmap;

		public static readonly StyledProperty<string> UriSourceProperty =
			AvaloniaProperty.Register<BitmapImage, string>(nameof(UriSource));

		/// <summary>图像资源的 URI（如 "/Assets/icon.png"）。</summary>
		public string UriSource
		{
			get => GetValue(UriSourceProperty);
			set => SetValue(UriSourceProperty, value);
		}

		public Size Size => _bitmap?.Size ?? default(Size);

		static BitmapImage()
		{
			UriSourceProperty.Changed.AddClassHandler<BitmapImage>((s, e) => s.OnUriSourceChanged(e));
		}

		private void OnUriSourceChanged(AvaloniaPropertyChangedEventArgs e)
		{
			LoadBitmap();
		}

		private void LoadBitmap()
		{
			string uri = UriSource;
			if (string.IsNullOrEmpty(uri))
			{
				_bitmap = null;
				return;
			}
			try
			{
				Uri u = new Uri(uri, UriKind.RelativeOrAbsolute);
				using (System.IO.Stream stream = AssetLoader.Open(u))
				{
					_bitmap = new Bitmap(stream);
				}
			}
			catch
			{
				_bitmap = null;
			}
		}

		public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
		{
			if (_bitmap == null)
			{
				return;
			}
			// Avalonia Bitmap 没有 Draw 方法，用 DrawingContext.DrawImage 渲染。
			// 当 sourceRect 与 _bitmap.Size 一致时使用 destRect 重载；否则用源/目标 Rect 重载。
			if (sourceRect == default || sourceRect.Size == _bitmap.Size)
			{
				context.DrawImage(_bitmap, destRect);
			}
			else
			{
				context.DrawImage(_bitmap, sourceRect, destRect);
			}
		}
	}
}
