using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	/// <summary>
	/// v4.3.1：标记"指针下是图片区域且可缩放"的控件。ScrollViewer 的滚轮处理
	/// （ScrollViewerWheelFix / TouchpadAwareScrollViewer 都是 Tunnel 阶段、先于图片控件收到事件）
	/// 据此放行滚轮：落在图片上时交给图片控件缩放，而不是滚动页面。
	/// </summary>
	public interface IZoomableImage
	{
		/// <summary>当前是否真的能在本控件上缩放（无图片时为 false，滚轮应回落为滚动页面）。</summary>
		bool IsZoomable { get; }
	}

	/// <summary>
	/// v4.3.1：并排（Side-by-Side）视图里承载单张图片的可缩放/平移控件，取代原先的 Viewbox。
	/// 首屏贴合行为由 <see cref="ImageZoomState.FitScale"/> 复刻（不放大超过原始像素，等价原
	/// Viewbox 的 MaxHeight=像素高）；左右两栏共用同一个 <see cref="ImageZoomState"/> 实例，
	/// 滚轮缩放/拖动平移天然同步，并按归一化中心点对齐（图片尺寸不一致也能对上同一图像位置）。
	/// 基类用 Control（Panel.Render 已 sealed 无法自绘），在 Render 里先铺一层透明矩形——
	/// 与 Panel(Background=Transparent) 等效，保证整块区域（含图片外的留白）可命中，
	/// 滚轮/拖动在整个图片区域都有效。
	/// </summary>
	public class ZoomPanImageControl : Control, IZoomableImage
	{
		private const double PanSlack = 0.5;

		[Null]
		private ImageZoomState _zoomState;

		[Null]
		private Bitmap _source;

		[Null]
		private Bitmap _diffSource;

		[Null]
		private AnimatedImagePlayer _player;

		private bool _highlightImageDiff;

		private bool _dragging;

		private double _dragStartZoom = 1.0;

		private double _dragStartCenterX = 0.5;

		private double _dragStartCenterY = 0.5;

		private Point _dragStartPointer;

		[Null]
		private Cursor _previousCursor;

		public ZoomPanImageControl()
		{
			ClipToBounds = true;
			AddHandler(PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Bubble);
			AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
			AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
			AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);
		}

		/// <summary>有图可缩放时才为 true（见 <see cref="IZoomableImage"/>）。</summary>
		public bool IsZoomable => CurrentBitmap != null;

		/// <summary>v4.3.2：动图播放器。装配后按当前帧渲染，帧切换自动重绘；
		/// 动图上不叠加像素差异掩码（关闭动图像素差异高亮）。</summary>
		[Null]
		public AnimatedImagePlayer Player
		{
			get
			{
				return _player;
			}
			set
			{
				if (ReferenceEquals(_player, value))
				{
					return;
				}
				if (_player != null)
				{
					_player.FrameChanged -= OnPlayerFrameChanged;
				}
				_player = value;
				if (_player != null)
				{
					_player.FrameChanged += OnPlayerFrameChanged;
				}
				InvalidateMeasure();
				InvalidateVisual();
			}
		}

		/// <summary>当前要绘制的位图：动图取当前帧，否则取静态源图。</summary>
		[Null]
		private Bitmap CurrentBitmap
		{
			get
			{
				if (_player != null && _player.Image.FrameCount > 0)
				{
					int index = System.Math.Clamp(_player.CurrentFrame, 0, _player.Image.FrameCount - 1);
					return _player.Image.Frames[index];
				}
				return _source;
			}
		}

		/// <summary>与其它视图共享的缩放/平移状态（同一实例即同步）。</summary>
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

		[Null]
		public Bitmap Source
		{
			get
			{
				return _source;
			}
			set
			{
				_source = value;
				InvalidateMeasure();
				InvalidateVisual();
			}
		}

		[Null]
		public Bitmap DiffSource
		{
			get
			{
				return _diffSource;
			}
			set
			{
				_diffSource = value;
				InvalidateVisual();
			}
		}

		/// <summary>高亮像素差异开关（叠加 <see cref="DiffSource"/> 掩码）。</summary>
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

		private Size ImageSize
		{
			get
			{
				Bitmap bitmap = CurrentBitmap;
				return ((bitmap != null) ? new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height) : default(Size));
			}
		}

		/// <summary>首屏期望尺寸 = 图片按贴合比例缩放后的尺寸（等价原 Viewbox 的测量结果）。</summary>
		protected override Size MeasureOverride(Size availableSize)
		{
			Size image = ImageSize;
			if (image.Width <= 0.0 || image.Height <= 0.0 || availableSize.Width <= 0.0 || availableSize.Height <= 0.0)
			{
				return default(Size);
			}
			double scale = ImageZoomState.FitScale(availableSize, image);
			return new Size(image.Width * scale, image.Height * scale);
		}

		public override void Render(DrawingContext context)
		{
			base.Render(context);
			// 基类 Control 不自绘 Background：显式铺透明矩形，保证整块区域可命中
			//（滚轮/拖动在图片外的留白处同样有效，等价 Panel(Background=Transparent)）。
			context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
			Bitmap bitmap = CurrentBitmap;
			if (bitmap == null)
			{
				return;
			}
			Rect rect = DstRect();
			context.DrawImage(bitmap, rect);
			// v4.3.2：动图不叠加像素差异掩码（GIF 逐帧差异无意义，直接关闭该侧高亮）。
			if (_highlightImageDiff && _diffSource != null && _player == null)
			{
				context.DrawImage(_diffSource, rect);
			}
		}

		private Rect DstRect()
		{
			return ImageZoomState.ComputeDstRect(Bounds.Size, ImageSize,
				_zoomState?.Zoom ?? 1.0, _zoomState?.CenterX ?? 0.5, _zoomState?.CenterY ?? 0.5);
		}

		private bool CanPan()
		{
			if (CurrentBitmap == null || _zoomState == null)
			{
				return false;
			}
			Rect rect = DstRect();
			return rect.Width > Bounds.Width + PanSlack || rect.Height > Bounds.Height + PanSlack;
		}

		private void OnZoomStateChanged(object sender, System.EventArgs e)
		{
			InvalidateVisual();
		}

		private void OnPlayerFrameChanged(object sender, System.EventArgs e)
		{
			InvalidateVisual();
		}

		private void OnPointerWheel(object sender, PointerWheelEventArgs e)
		{
			if (e.Handled || CurrentBitmap == null || _zoomState == null)
			{
				return;
			}
			double delta = e.Delta.Y;
			if (System.Math.Abs(delta) < 0.01)
			{
				return;
			}
			_zoomState.ZoomAt(Bounds.Size, ImageSize, e.GetPosition(this), delta);
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
			if (!_dragging || CurrentBitmap == null || _zoomState == null)
		{
			return;
		}
		_zoomState.PanTo(Bounds.Size, ImageSize, _dragStartZoom,
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
	}
}