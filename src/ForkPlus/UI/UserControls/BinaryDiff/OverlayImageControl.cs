using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public class OverlayImageControl : Control
	{
		private enum HorizontalClip
		{
			Old,
			New
		}

		private global::Avalonia.Media.Imaging.Bitmap _oldImageSource;

		private global::Avalonia.Media.Imaging.Bitmap _newImageSource;

		[Null]
		private global::Avalonia.Media.Imaging.Bitmap _diffImageSource;

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

		public void SetContent(global::Avalonia.Media.Imaging.Bitmap oldImageSource, global::Avalonia.Media.Imaging.Bitmap newImageSource, [Null] global::Avalonia.Media.Imaging.Bitmap diffImageSource)
	{
		// Migration note：WPF Control.Background 在 Avalonia 的 Control 基类不存在（且原码从未渲染它，属死代码），移除。
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

		private void Draw(DrawingContext drawingContext, global::Avalonia.Media.Imaging.Bitmap image, Rect imageRect, HorizontalClip clipKind, double? clipX, double? opacity = null)
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
			// Migration note：WPF Push/Pop 配对 → Avalonia Push* 返回 struct PushedState，
		// 不能与 null 组成条件表达式；default(PushedState).Dispose() 是判空安全的空操作，用它替代 null 分支。
		// PushClip(RectangleGeometry) → PushGeometryClip(Geometry)。
		using (rectangleGeometry != null ? drawingContext.PushGeometryClip(rectangleGeometry) : default(global::Avalonia.Media.DrawingContext.PushedState))
		using (opacity.HasValue ? drawingContext.PushOpacity(opacity.Value) : default(global::Avalonia.Media.DrawingContext.PushedState))
		{
			drawingContext.DrawImage(image, imageRect);
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

		private static Size ResizeImageMaintaningAspectRatio(global::Avalonia.Media.Imaging.Bitmap image, Size targetSize)
		{
			if ((double)image.PixelSize.Width < targetSize.Width && (double)image.PixelSize.Height < targetSize.Height)
			{
				return new Size(image.PixelSize.Width, image.PixelSize.Height);
			}
			double num = targetSize.Width / (double)image.PixelSize.Width;
			double num2 = targetSize.Height / (double)image.PixelSize.Height;
			if (!(num < num2))
			{
				return new Size(Math.Floor((double)image.PixelSize.Width * num2), Math.Floor((double)image.PixelSize.Height * num2));
			}
			return new Size(Math.Floor((double)image.PixelSize.Width * num), Math.Floor((double)image.PixelSize.Height * num));
		}
	}
}
