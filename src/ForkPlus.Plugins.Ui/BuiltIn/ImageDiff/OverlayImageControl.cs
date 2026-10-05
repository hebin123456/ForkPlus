using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	/// <summary>
	/// v4.3.1：Swipe / 洋葱皮视图的叠加渲染控件。除原有的分割裁剪/透明度叠加外，
	/// 新增滚轮缩放 + 拖动平移（与并排视图共享同一个 <see cref="ImageZoomState"/>）：
	/// 两张图按各自的归一化中心点绘制，缩放/平移后仍保持同一图像位置对齐。
	/// 基类用 Control（Panel.Render 已 sealed 无法自绘），在 Render 里先铺一层透明矩形——
	/// 与 Panel(Background=Transparent) 等效，保证整块区域（含图片外的留白）可命中，
	/// 滚轮/拖动在整个图片区域都有效。
	/// </summary>
	public class OverlayImageControl : Control, IZoomableImage
	{
		private const double PanSlack = 0.5;

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

		[Null]
		private ImageZoomState _zoomState;

		private bool _dragging;

		private double _dragStartZoom = 1.0;

		private double _dragStartCenterX = 0.5;

		private double _dragStartCenterY = 0.5;

		private Point _dragStartPointer;

		[Null]
		private Cursor _previousCursor;

		public OverlayImageControl()
		{
			ClipToBounds = true;
			AddHandler(PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Bubble);
			AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
			AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
			AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);
		}

		/// <summary>两张图都在时才可缩放（见 <see cref="IZoomableImage"/>）。</summary>
		public bool IsZoomable => _oldImageSource != null && _newImageSource != null;

		/// <summary>与并排视图共享的缩放/平移状态（同一实例即同步）。</summary>
		[Null]
		public ImageZoomState ZoomState
		{
			get
			{
				return _zoomState;
			}
			set
			{
				if (ReferenceEquals(_zoomState, value))
				{
					return;
				}
				if (_zoomState != null)
				{
					_zoomState.Changed -= OnZoomStateChanged;
				}
				_zoomState = value;
				if (_zoomState != null)
				{
					_zoomState.Changed += OnZoomStateChanged;
				}
				InvalidateVisual();
			}
		}

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

		/// <summary>v4.3.1：Measure 仍按"两张图各自贴合 ParentBounds 后取较大者"给出期望尺寸
		/// （与改造前一致）；实际绘制视口用 <see cref="Visual.Bounds"/>（见 Render），
		/// 缩放/平移几何统一由 <see cref="ImageZoomState"/> 计算。</summary>
		protected override Size MeasureOverride(Size availableSize)
		{
			if (_oldImageSource == null || _newImageSource == null)
			{
				return new Size(0.0, 0.0);
			}
			Size oldImageSize = ResizeImageMaintaningAspectRatio(_oldImageSource, ParentBounds);
			Size newImageSize = ResizeImageMaintaningAspectRatio(_newImageSource, ParentBounds);
			double width = Math.Max(oldImageSize.Width, newImageSize.Width);
			double height = Math.Max(oldImageSize.Height, newImageSize.Height);
			return new Size(width, height);
		}

		public override void Render(DrawingContext drawingContext)
		{
			base.Render(drawingContext);
			// 基类 Control 不自绘 Background：显式铺透明矩形，保证整块区域可命中
			//（滚轮/拖动在图片外的留白处同样有效，等价改造前的 Panel(Background=Transparent)）。
			drawingContext.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
			if (_oldImageSource != null && _newImageSource != null)
			{
				// v4.3.1：两张图各自按共享的缩放/中心点算出目标矩形——中心点用归一化
				// 图像坐标表达，尺寸不同的两张图也能对齐同一图像位置（中心点对齐）。
				Rect imageRect = DstRectFor(_oldImageSource);
				Draw(drawingContext, _oldImageSource, imageRect, HorizontalClip.Old, ClipX);
				Rect imageRect2 = DstRectFor(_newImageSource);
				Draw(drawingContext, _newImageSource, imageRect2, HorizontalClip.New, ClipX, NewOpacity);
			if (HighlightImageDiff && _diffImageSource != null)
			{
				// 修复（2026-09-14，高亮像素双侧显示）：掩码此前只画在 new 图矩形（分割线右侧），
				// 左图的高亮像素被显示到了右边。改为左右各画一份：Old 掩码随左图裁剪/透明度、
				// New 掩码随右图裁剪/透明度——Swipe 模式分割线两侧各自高亮，洋葱皮模式两份全幅叠加。
				Draw(drawingContext, _diffImageSource, imageRect, HorizontalClip.Old, ClipX);
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

		/// <summary>图片在控件坐标系下的目标矩形（贴合基准 + 共享缩放 + 归一化中心点）。</summary>
		private Rect DstRectFor([Null] global::Avalonia.Media.Imaging.Bitmap image)
		{
			return ImageZoomState.ComputeDstRect(Bounds.Size, PixelSize(image),
				_zoomState?.Zoom ?? 1.0, _zoomState?.CenterX ?? 0.5, _zoomState?.CenterY ?? 0.5);
		}

		private static Size PixelSize([Null] global::Avalonia.Media.Imaging.Bitmap image)
		{
			return image != null ? new Size(image.PixelSize.Width, image.PixelSize.Height) : default(Size);
		}

		/// <summary>v4.3.1：滚轮缩放/拖动平移的参考图——取"贴合后显示面积"较大的那张：
		/// 钳制（ClampCenter）按它算，保证较大的图能拖到底；较小的一张若未超出视口则自然居中。</summary>
		private Size ReferenceImageSize()
		{
			Size viewport = Bounds.Size;
			Size oldPixel = PixelSize(_oldImageSource);
			Size newPixel = PixelSize(_newImageSource);
			return DisplayArea(viewport, newPixel) > DisplayArea(viewport, oldPixel) ? newPixel : oldPixel;
		}

		private static double DisplayArea(Size viewport, Size image)
		{
			if (image.Width <= 0.0 || image.Height <= 0.0)
			{
				return 0.0;
			}
			double fit = ImageZoomState.FitScale(viewport, image);
			return image.Width * fit * image.Height * fit;
		}

		private bool CanPan()
		{
			if (!IsZoomable || _zoomState == null)
			{
				return false;
			}
			Size viewport = Bounds.Size;
			Rect oldRect = DstRectFor(_oldImageSource);
			Rect newRect = DstRectFor(_newImageSource);
			return oldRect.Width > viewport.Width + PanSlack || oldRect.Height > viewport.Height + PanSlack
				|| newRect.Width > viewport.Width + PanSlack || newRect.Height > viewport.Height + PanSlack;
		}

		private void OnZoomStateChanged(object sender, EventArgs e)
		{
			InvalidateVisual();
		}

		private void OnPointerWheel(object sender, PointerWheelEventArgs e)
		{
			if (e.Handled || !IsZoomable || _zoomState == null)
			{
				return;
			}
			double delta = e.Delta.Y;
			if (Math.Abs(delta) < 0.01)
			{
				return;
			}
			_zoomState.ZoomAt(Bounds.Size, ReferenceImageSize(), e.GetPosition(this), delta);
			e.Handled = true;
		}

		private void OnPointerPressed(object sender, PointerPressedEventArgs e)
		{
			if (_dragging || !CanPan())
			{
				return;
			}
			PointerPoint point = e.GetCurrentPoint(this);
			if (!point.Properties.IsLeftButtonPressed)
			{
				return;
			}
			_dragging = true;
			_dragStartPointer = point.Position;
			_dragStartZoom = _zoomState.Zoom;
			_dragStartCenterX = _zoomState.CenterX;
			_dragStartCenterY = _zoomState.CenterY;
			_previousCursor = Cursor;
			Cursor = new Cursor(StandardCursorType.SizeAll);
			e.Pointer.Capture(this);
			e.Handled = true;
		}

		private void OnPointerMoved(object sender, PointerEventArgs e)
		{
			if (!_dragging || _zoomState == null)
			{
				return;
			}
			_zoomState.PanTo(Bounds.Size, ReferenceImageSize(), _dragStartZoom,
				_dragStartCenterX, _dragStartCenterY, _dragStartPointer, e.GetPosition(this));
			e.Handled = true;
		}

		private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
		{
			if (!_dragging)
			{
				return;
			}
			_dragging = false;
			e.Pointer.Capture(null);
			Cursor = _previousCursor;
			e.Handled = true;
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
