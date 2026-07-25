using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF FrameworkElement.OnRender → Avalonia.Control.Render。
	// WPF PointerEventArgs/OnMouseDown → Avalonia PointerEventArgs/OnPointerPressed。
	// WPF Mouse.GetPosition 静态方法不存在于 Avalonia；改为在 OnPointerMoved 中缓存最新位置。
	// WPF GuidelineSet 在 Avalonia 中不存在（Avalonia 用 UseLayoutRounding + SnapToPixels 像素对齐）；
	// 此处移除 GuidelineSet，依赖 Avalonia 默认像素对齐。
	// WPF StreamGeometryContext.BeginFigure/LineTo/BezierTo 带 isStroked/isClosed/isSmoothJoin 重载
	// → Avalonia 简化签名（仅点 + isFilled）。
	public class GraphCellView : Control
	{
		private static readonly double _defaultCellHeight;

		private static readonly double _defaultCellWidth;

		private static readonly double _commitPointRadius;

		private static readonly double _commitMergePointRadius;

		private static readonly double _chevronSize;

		private static readonly double _penThickness;

		private static readonly Pen _mouseOverPen;

		private static readonly string[] _branchColors;

		private static readonly Pen[] _branchPens;

		private readonly DispatcherTimer _showPopupTimer = new DispatcherTimer();

		private readonly DispatcherTimer _closePopupTimer = new DispatcherTimer();

		// 阶段 4.5：Avalonia 无静态 Mouse.GetPosition；缓存最近一次 PointerMoved 的位置。
		private Point _lastPointerPosition;

		private bool _isPopupMouseOver;

		[Null]
		private Popup _popup;

		private Sha? _activeMergePointSha;

		public static readonly StyledProperty<double> CellHeightProperty;

		public static readonly StyledProperty<bool> ShowGraphToolTipProperty;

		private bool _isMouseOver;

		public double CellHeight
		{
			get => GetValue(CellHeightProperty);
			set => SetValue(CellHeightProperty, value);
		}

		public bool ShowGraphToolTip
		{
			get => GetValue(ShowGraphToolTipProperty);
			set => SetValue(ShowGraphToolTipProperty, value);
		}

		private new bool IsMouseOver
		{
			get => _isMouseOver;
			set
			{
				if (_isMouseOver != value)
				{
					_isMouseOver = value;
					InvalidateVisual();
				}
			}
		}

		public event EventHandler ExpandToggle;

		static GraphCellView()
		{
			_defaultCellHeight = 22.0;
			_defaultCellWidth = 12.0;
			_commitPointRadius = 1.7;
			_commitMergePointRadius = 5.75;
			_chevronSize = 3.5;
			_penThickness = 1.5;
			// 阶段 4.5：WPF ColorConverter.ConvertFromString → Avalonia Color.Parse。
			_mouseOverPen = new Pen(new SolidColorBrush(Color.Parse("#0092FF")), 2.0);
			_branchColors = new string[13]
			{
				"#FF9502", "#FFCC00", "#FF3B30", "#A2845E", "#64DA38", "#1CADF8", "#CB73E1", "#8E8E91", "#FF2968", "#30D5C8",
				"#5856D6", "#B4D435", "#FF6F61"
			};
			_branchPens = _branchColors.Map((string c) => new Pen(new SolidColorBrush(Color.Parse(c)), _penThickness));
			CellHeightProperty = AvaloniaProperty.Register<GraphCellView, double>(nameof(CellHeight), _defaultCellHeight);
			ShowGraphToolTipProperty = AvaloniaProperty.Register<GraphCellView, bool>(nameof(ShowGraphToolTip), true);
			// 阶段 4.5：Avalonia 画刷/Pen 默认不可变，无需 WPF Freeze()。
		}

		public GraphCellView()
		{
			// 阶段 4.5：WPF SnapsToDevicePixels → Avalonia UseLayoutRounding。
			base.UseLayoutRounding = true;
			if (ShowGraphToolTip)
			{
				_showPopupTimer.Interval = TimeSpan.FromMilliseconds(600.0);
				_closePopupTimer.Interval = TimeSpan.FromMilliseconds(200.0);
				_showPopupTimer.Tick += _showPopupTimer_Tick;
				_closePopupTimer.Tick += _closePopupTimer_Tick;
			}
		}

		protected override void OnPointerEntered(PointerEventArgs e)
		{
			e.Handled = true;
			if (ShowGraphToolTip && base.DataContext is DecoratedRevision decoratedRevision && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				base.OnPointerEntered(e);
				if (decoratedRevision.GetParents().Length > 1)
				{
					_activeMergePointSha = decoratedRevision.Sha;
					_showPopupTimer.Start();
					_closePopupTimer.Stop();
				}
			}
		}

		protected override void OnPointerExited(PointerEventArgs e)
		{
			e.Handled = true;
			base.OnPointerExited(e);
			IsMouseOver = false;
			if (ShowGraphToolTip && base.DataContext is DecoratedRevision decoratedRevision && decoratedRevision.GetParents().Length > 1)
			{
				_activeMergePointSha = null;
				_showPopupTimer.Stop();
				_closePopupTimer.Start();
			}
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			e.Handled = true;
			base.OnPointerMoved(e);
			_lastPointerPosition = e.GetPosition(this);
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				int num = (int)((_lastPointerPosition.X + 5.0) / _defaultCellWidth);
				IsMouseOver = num == decoratedRevision.GraphInfo.CurrentCommitColumn;
			}
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			e.Handled = true;
			base.OnPointerPressed(e);
			// 阶段 4.5：WPF e.ChangedButton == MouseButton.Left → Avalonia PointerPointProperties.IsLeftButtonPressed。
			if (IsMouseOver && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && base.DataContext is DecoratedRevision decoratedRevision)
			{
				this.ExpandToggle?.Invoke(this, EventArgs.Empty);
				if (ShowGraphToolTip && decoratedRevision.GetParents().Length > 1)
				{
					_activeMergePointSha = null;
					ClosePopup(hardClose: true);
					_showPopupTimer.Stop();
				}
			}
		}

		// 阶段 4.5：WPF FrameworkElement.OnRender → Avalonia Control.Render。
		public override void Render(DrawingContext drawingContext)
		{
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				base.Width = _defaultCellWidth * (double)decoratedRevision.GraphInfo.Lines.Length;
				GraphLine[] lines = decoratedRevision.GraphInfo.Lines;
				foreach (GraphLine line in lines)
				{
					DrawLine(drawingContext, line, _defaultCellWidth);
				}
				bool isMergeCommit = decoratedRevision.GetParents().Length > 1;
				bool isCollapsed = decoratedRevision.IsCollapsed;
				DrawCommitPoint(drawingContext, decoratedRevision.GraphInfo, _defaultCellWidth, isMergeCommit, isCollapsed);
			}
			base.Render(drawingContext);
		}

		private void _showPopupTimer_Tick(object sender, EventArgs e)
		{
			ShowPopup();
			_showPopupTimer.Stop();
		}

		private void _closePopupTimer_Tick(object sender, EventArgs e)
		{
			ClosePopup();
			_closePopupTimer.Stop();
		}

		private void DrawLine(DrawingContext drawingContext, GraphLine line, double columnWidth)
		{
			double num = 0.0;
			Point point = new Point(num + columnWidth * (double)(int)line.Column, CellHeight / 2.0);
			Pen pen = _branchPens[line.Id % _branchPens.Length];
			if (line.TopColumn != byte.MaxValue)
			{
				Point point2 = new Point(num + columnWidth * (double)(int)line.TopColumn, 0.0);
				if (line.BottomColumn != byte.MaxValue)
				{
					Point point3 = new Point(num + columnWidth * (double)(int)line.BottomColumn, CellHeight);
					if (line.TopColumn == line.BottomColumn)
					{
						drawingContext.DrawLine(pen, point2, point3);
						return;
					}
					StreamGeometry streamGeometry = new StreamGeometry();
					using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
					{
						// 阶段 4.5：Avalonia BeginFigure(Point, bool isFilled) — 无 isClosed 参数。
						streamGeometryContext.BeginFigure(point2, isFilled: false);
						// 阶段 4.5：Avalonia BezierTo(Point, Point, Point) — 无 isStroked/isSmoothJoin 参数。
						streamGeometryContext.BezierTo(new Point(point2.X, point3.Y - 5.0), new Point(point3.X, point2.Y + 5.0), point3);
					}
					drawingContext.DrawGeometry(null, pen, streamGeometry);
				}
				else if (line.TopColumn == line.Column)
				{
					drawingContext.DrawLine(pen, point2, point);
				}
				else
				{
					StreamGeometry streamGeometry2 = new StreamGeometry();
					using (StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open())
					{
						streamGeometryContext2.BeginFigure(point2, isFilled: false);
						streamGeometryContext2.BezierTo(new Point(point2.X, point.Y), new Point(point.X + 5.0, point.Y), point);
					}
					drawingContext.DrawGeometry(null, pen, streamGeometry2);
				}
			}
			else
			{
				if (line.BottomColumn == byte.MaxValue)
				{
					return;
				}
				Point point4 = new Point(num + columnWidth * (double)(int)line.BottomColumn, CellHeight);
				if (line.Column == line.BottomColumn)
				{
					drawingContext.DrawLine(pen, point, point4);
					return;
				}
				StreamGeometry streamGeometry3 = new StreamGeometry();
				using (StreamGeometryContext streamGeometryContext3 = streamGeometry3.Open())
				{
					streamGeometryContext3.BeginFigure(point, isFilled: false);
					streamGeometryContext3.BezierTo(new Point(point4.X, point.Y), new Point(point4.X, point.Y + 5.0), point4);
				}
				drawingContext.DrawGeometry(null, pen, streamGeometry3);
			}
		}

		private void DrawCommitPoint(DrawingContext drawingContext, GraphInfo graphInfo, double cellWidth, bool isMergeCommit, bool isCollapsed)
		{
			if (graphInfo.CurrentCommitLineId < 0)
			{
				return;
			}
			Pen pen = _branchPens[graphInfo.CurrentCommitLineId % _branchPens.Length];
			Point center = new Point(cellWidth * (double)(int)graphInfo.CurrentCommitColumn, CellHeight / 2.0);
			if (!isMergeCommit)
			{
				drawingContext.DrawEllipse(pen.Brush, pen, center, _commitPointRadius, _commitPointRadius);
				return;
			}
			Pen pen2 = (IsMouseOver ? _mouseOverPen : pen);
			drawingContext.DrawEllipse(Theme.RevisionList.ItemBackgroundBrush, pen2, center, _commitMergePointRadius, _commitMergePointRadius);
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				if (isCollapsed)
				{
					streamGeometryContext.BeginFigure(new Point(center.X - _chevronSize * 0.5, center.Y - _chevronSize), isFilled: false);
					streamGeometryContext.LineTo(new Point(center.X + _chevronSize * 0.5, center.Y));
					streamGeometryContext.LineTo(new Point(center.X - _chevronSize * 0.5, center.Y + _chevronSize));
				}
				else
				{
					streamGeometryContext.BeginFigure(new Point(center.X - _chevronSize, center.Y - _chevronSize * 0.5), isFilled: false);
					streamGeometryContext.LineTo(new Point(center.X, center.Y + _chevronSize * 0.5));
					streamGeometryContext.LineTo(new Point(center.X + _chevronSize, center.Y - _chevronSize * 0.5));
				}
			}
			drawingContext.DrawGeometry(null, pen, streamGeometry);
		}

		private void ShowPopup()
		{
			RepositoryUserControl parent = this.GetParent<RepositoryUserControl>();
			if (parent != null && (_popup == null || !_popup.IsOpen))
			{
				Sha? activeMergePointSha = _activeMergePointSha;
				if (activeMergePointSha.HasValue)
				{
					Sha valueOrDefault = activeMergePointSha.GetValueOrDefault();
					// 阶段 4.5：Avalonia 无静态 Mouse.GetPosition；使用最近一次 PointerMoved 缓存的位置。
					double horizontalOffset = _lastPointerPosition.X + 5.0;
					_popup = CreatePopup(parent, horizontalOffset, valueOrDefault);
					_popup.IsOpen = true;
				}
			}
		}

		private void ClosePopup(bool hardClose = false)
		{
			// 阶段 4.5：WPF popup.IsMouseOver 不存在于 Avalonia；改用缓存标志 _isPopupMouseOver。
			if (_popup != null && _popup.IsOpen && (!_isPopupMouseOver || hardClose))
			{
				_popup.IsOpen = false;
				VisualTreeAttachmentHelper.TrySetPopupChild(_popup, null, GetType().Name + ".Popup");
				_popup = null;
			}
		}

		private Popup CreatePopup(RepositoryUserControl repositoryUserControl, double horizontalOffset, Sha sha)
		{
			Popup popup = new Popup();
			popup.HorizontalOffset = horizontalOffset;
			popup.VerticalOffset = -50.0;
			// 阶段 4.5：WPF StaysOpen=true → Avalonia IsLightDismissEnabled=false（不点击外部关闭）。
			popup.IsLightDismissEnabled = false;
			// 阶段 4.5：WPF AllowsTransparency / PopupAnimation.Fade 在 Avalonia 中无对应；
			// Popup 默认透明，淡入动画需要时可通过 ControlTheme 添加。
			popup.PlacementTarget = this;
			RevisionGraphTooltipUserControl revisionGraphTooltipUserControl = new RevisionGraphTooltipUserControl(repositoryUserControl, sha);
			revisionGraphTooltipUserControl.HeightChanged += delegate(object s, EventArgs<double> e)
			{
				double value = e.Value;
				popup.VerticalOffset = 0.0 - value / 2.0 - 10.0;
			};
			// 阶段 4.5：WPF popup.MouseLeave → 在 popup.Child 上订阅 PointerExited（Popup 自身不接收指针事件）。
			// 阶段 4.5：WPF popup.IsMouseOver → 手动跟踪 _isPopupMouseOver。
			revisionGraphTooltipUserControl.PointerEntered += delegate
			{
				_isPopupMouseOver = true;
				_closePopupTimer.Stop();
			};
			revisionGraphTooltipUserControl.PointerExited += delegate
			{
				_isPopupMouseOver = false;
				_closePopupTimer.Start();
			};
			VisualTreeAttachmentHelper.TrySetPopupChild(popup, revisionGraphTooltipUserControl, GetType().Name + ".Popup");
			return popup;
		}
	}
}
