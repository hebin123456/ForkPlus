using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public class OverlayImageControl : Control
	{
		private enum HorizontalClip
		{
			Old,
			New
		}

		private BitmapSource _oldImageSource;

		private BitmapSource _newImageSource;

		[Null]
		private BitmapSource _diffImageSource;

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

		public void SetContent(BitmapSource oldImageSource, BitmapSource newImageSource, [Null] BitmapSource diffImageSource)
		{
			base.Background = Brushes.Red;
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

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);
			if (_oldImageSource != null && _newImageSource != null)
			{
				Rect targetRect = new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight);
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

		private void Draw(DrawingContext drawingContext, BitmapSource image, Rect imageRect, HorizontalClip clipKind, double? clipX, double? opacity = null)
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

		private static Size ResizeImageMaintaningAspectRatio(BitmapSource image, Size targetSize)
		{
			if ((double)image.PixelWidth < targetSize.Width && (double)image.PixelHeight < targetSize.Height)
			{
				return new Size(image.PixelWidth, image.PixelHeight);
			}
			double num = targetSize.Width / (double)image.PixelWidth;
			double num2 = targetSize.Height / (double)image.PixelHeight;
			if (!(num < num2))
			{
				return new Size(Math.Floor((double)image.PixelWidth * num2), Math.Floor((double)image.PixelHeight * num2));
			}
			return new Size(Math.Floor((double)image.PixelWidth * num), Math.Floor((double)image.PixelHeight * num));
		}
	}
}
