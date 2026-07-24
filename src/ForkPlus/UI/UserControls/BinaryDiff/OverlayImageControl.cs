// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Media → using Avalonia.Media
// - using System.Windows.Media.Imaging → using Bitmap = Avalonia.Media.Imaging.Bitmap（别名替代 BitmapSource）
// - BitmapSource → Bitmap（Avalonia.Media.Imaging.Bitmap，参考 IconTools）
// - OnRender(DrawingContext) → Render(DrawingContext)（Avalonia 自定义控件绘制重写方法名）
// - base.Background = Brushes.Red → 移除（Avalonia Control 无 Background 属性；该方法仅绘制图像，不依赖背景色，参考 DrawingContext 渲染流程）
// - base.ActualWidth/base.ActualHeight → base.Bounds.Width/base.Bounds.Height
// - image.PixelWidth/image.PixelHeight → image.PixelSize.Width/image.PixelSize.Height（Avalonia Bitmap 用 PixelSize 描述像素尺寸）
// - InvalidateMeasure/InvalidateVisual → API 兼容（Avalonia.Layoutable/Avalonia.Visual）
// - MeasureOverride(Size) → API 兼容（Avalonia.Layoutable.MeasureOverride）
// - drawingContext.DrawImage(image, imageRect) → API 兼容（Bitmap 实现 IImage）
// - drawingContext.PushClip(RectangleGeometry)/PushOpacity(double)/Pop() → API 兼容
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public class OverlayImageControl : Control
	{
		private enum HorizontalClip
		{
			Old,
			New
		}

		private Bitmap _oldImageSource;

		private Bitmap _newImageSource;

		[Null]
		private Bitmap _diffImageSource;

		private Size _parentBounds;

		private bool _highlightImageDiff;

		private double? _clipX;

		private double? _newOpacity;

		private Size _oldImageSize;

		private Size _newImageSize;

		public Size ParentBounds
		{
			get
			{
				return _parentBounds;
			}
			set
			{
				_parentBounds = value;
				InvalidateMeasure();
			}
		}

		public bool HighlightImageDiff
		{
			get
			{
				return _highlightImageDiff;
			}
			set
			{
				_highlightImageDiff = value;
				InvalidateVisual();
			}
		}

		public double? ClipX
		{
			get
			{
				return _clipX;
			}
			set
			{
				_clipX = value;
				InvalidateVisual();
			}
		}

		public double? NewOpacity
		{
			get
			{
				return _newOpacity;
			}
			set
			{
				_newOpacity = value;
				InvalidateVisual();
			}
		}

		public void SetContent(Bitmap oldImageSource, Bitmap newImageSource, [Null] Bitmap diffImageSource)
		{
			_oldImageSource = oldImageSource;
			_newImageSource = newImageSource;
			_diffImageSource = diffImageSource;
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			if (_oldImageSource == null || _newImageSource == null)
			{
				return new Size(0.0, 0.0);
			}
			Size oldImageSize = ResizeImageMaintaningAspectRatio(_oldImageSource, ParentBounds);
			Size newImageSize = ResizeImageMaintaningAspectRatio(_newImageSource, ParentBounds);
			_oldImageSize = oldImageSize;
			_newImageSize = newImageSize;
			double width = Math.Max(oldImageSize.Width, newImageSize.Width);
			double height = Math.Max(oldImageSize.Height, newImageSize.Height);
			return new Size(width, height);
		}

		public override void Render(DrawingContext drawingContext)
		{
			base.Render(drawingContext);
			if (_oldImageSource != null && _newImageSource != null)
			{
				Rect targetRect = new Rect(0.0, 0.0, base.Bounds.Width, base.Bounds.Height);
				Rect imageRect = GetImageRect(_oldImageSize, targetRect);
				Draw(drawingContext, _oldImageSource, imageRect, HorizontalClip.Old, ClipX);
				Rect imageRect2 = GetImageRect(_newImageSize, targetRect);
				Draw(drawingContext, _newImageSource, imageRect2, HorizontalClip.New, ClipX, NewOpacity);
				if (HighlightImageDiff && _diffImageSource != null)
				{
					Draw(drawingContext, _diffImageSource, imageRect2, HorizontalClip.New, ClipX, NewOpacity);
				}
			}
		}

		private void Draw(DrawingContext drawingContext, Bitmap image, Rect imageRect, HorizontalClip clipKind, double? clipX, double? opacity = null)
		{
			RectangleGeometry rectangleGeometry = null;
			if (clipX.HasValue)
			{
				double valueOrDefault = clipX.GetValueOrDefault();
				switch (clipKind)
				{
				case HorizontalClip.Old:
					rectangleGeometry = new RectangleGeometry(new Rect(imageRect.X, imageRect.Y, valueOrDefault, imageRect.Height));
					break;
				case HorizontalClip.New:
					rectangleGeometry = new RectangleGeometry(new Rect(valueOrDefault, imageRect.Y, Math.Abs(imageRect.Width - valueOrDefault), imageRect.Height));
					break;
				}
			}
			if (rectangleGeometry != null)
			{
				drawingContext.PushClip(rectangleGeometry);
			}
			if (opacity.HasValue)
			{
				drawingContext.PushOpacity(opacity.Value);
			}
			drawingContext.DrawImage(image, imageRect);
			if (rectangleGeometry != null)
			{
				drawingContext.Pop();
			}
			if (opacity.HasValue)
			{
				drawingContext.Pop();
			}
		}

		private Rect GetImageRect(Size imageSize, Rect targetRect)
		{
			double y = 0.0;
			double x = 0.0;
			if (imageSize.Height < targetRect.Height)
			{
				y = (targetRect.Height - imageSize.Height) / 2.0;
			}
			if (imageSize.Width < targetRect.Width)
			{
				x = (targetRect.Width - imageSize.Width) / 2.0;
			}
			return new Rect(x, y, imageSize.Width, imageSize.Height);
		}

		private static Size ResizeImageMaintaningAspectRatio(Bitmap image, Size targetSize)
		{
			double pixelWidth = image.PixelSize.Width;
			double pixelHeight = image.PixelSize.Height;
			if (pixelWidth < targetSize.Width && pixelHeight < targetSize.Height)
			{
				return new Size(pixelWidth, pixelHeight);
			}
			double num = targetSize.Width / pixelWidth;
			double num2 = targetSize.Height / pixelHeight;
			if (!(num < num2))
			{
				return new Size(Math.Floor(pixelWidth * num2), Math.Floor(pixelHeight * num2));
			}
			return new Size(Math.Floor(pixelWidth * num), Math.Floor(pixelHeight * num));
		}
	}
}
