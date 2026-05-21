using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	public class GraphCellView : FrameworkElement
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

		[Null]
		private Popup _popup;

		private Sha? _activeMergePointSha;

		public static readonly DependencyProperty CellHeightProperty;

		public static readonly DependencyProperty ShowGraphToolTipProperty;

		private bool _isMouseOver;

		public double CellHeight
		{
			get
			{
				return (double)GetValue(CellHeightProperty);
			}
			set
			{
				SetValue(CellHeightProperty, value);
			}
		}

		public bool ShowGraphToolTip
		{
			get
			{
				return (bool)GetValue(ShowGraphToolTipProperty);
			}
			set
			{
				SetValue(ShowGraphToolTipProperty, value);
			}
		}

		private new bool IsMouseOver
		{
			get
			{
				return _isMouseOver;
			}
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
			_mouseOverPen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0092FF")), 2.0);
			_branchColors = new string[13]
			{
				"#FF9502", "#FFCC00", "#FF3B30", "#A2845E", "#64DA38", "#1CADF8", "#CB73E1", "#8E8E91", "#FF2968", "#30D5C8",
				"#5856D6", "#B4D435", "#FF6F61"
			};
			_branchPens = _branchColors.Map((string c) => new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)), _penThickness));
			CellHeightProperty = DependencyProperty.Register("CellHeight", typeof(double), typeof(GraphCellView), new FrameworkPropertyMetadata(_defaultCellHeight));
			ShowGraphToolTipProperty = DependencyProperty.Register("ShowGraphToolTip", typeof(bool), typeof(GraphCellView), new PropertyMetadata(true));
			Pen[] branchPens = _branchPens;
			for (int i = 0; i < branchPens.Length; i++)
			{
				branchPens[i].Freeze();
			}
			_mouseOverPen.Freeze();
		}

		public GraphCellView()
		{
			base.SnapsToDevicePixels = true;
			if (ShowGraphToolTip)
			{
				_showPopupTimer.Interval = TimeSpan.FromMilliseconds(600.0);
				_closePopupTimer.Interval = TimeSpan.FromMilliseconds(200.0);
				_showPopupTimer.Tick += _showPopupTimer_Tick;
				_closePopupTimer.Tick += _closePopupTimer_Tick;
			}
		}

		protected override void OnMouseEnter(MouseEventArgs e)
		{
			e.Handled = true;
			if (ShowGraphToolTip && base.DataContext is DecoratedRevision decoratedRevision && e.LeftButton != MouseButtonState.Pressed)
			{
				base.OnMouseEnter(e);
				if (decoratedRevision.GetParents().Length > 1)
				{
					_activeMergePointSha = decoratedRevision.Sha;
					_showPopupTimer.Start();
					_closePopupTimer.Stop();
				}
			}
		}

		protected override void OnMouseLeave(MouseEventArgs e)
		{
			e.Handled = true;
			base.OnMouseLeave(e);
			IsMouseOver = false;
			if (ShowGraphToolTip && base.DataContext is DecoratedRevision decoratedRevision && decoratedRevision.GetParents().Length > 1)
			{
				_activeMergePointSha = null;
				_showPopupTimer.Stop();
				_closePopupTimer.Start();
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			e.Handled = true;
			base.OnMouseMove(e);
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				int num = (int)((e.GetPosition(this).X + 5.0) / _defaultCellWidth);
				IsMouseOver = num == decoratedRevision.GraphInfo.CurrentCommitColumn;
			}
		}

		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			e.Handled = true;
			base.OnMouseDown(e);
			if (IsMouseOver && e.ChangedButton == MouseButton.Left && base.DataContext is DecoratedRevision decoratedRevision)
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

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				base.Width = _defaultCellWidth * (double)decoratedRevision.GraphInfo.Lines.Length;
				GuidelineSet guidelineSet = new GuidelineSet();
				GraphLine[] lines = decoratedRevision.GraphInfo.Lines;
				for (int i = 0; i < lines.Length; i++)
				{
					GraphLine graphLine = lines[i];
					AddColumnGuideline(guidelineSet, graphLine.Column);
					if (graphLine.TopColumn != byte.MaxValue)
					{
						AddColumnGuideline(guidelineSet, graphLine.TopColumn);
					}
					if (graphLine.BottomColumn != byte.MaxValue)
					{
						AddColumnGuideline(guidelineSet, graphLine.BottomColumn);
					}
				}
				guidelineSet.Freeze();
				drawingContext.PushGuidelineSet(guidelineSet);
				lines = decoratedRevision.GraphInfo.Lines;
				foreach (GraphLine line in lines)
				{
					DrawLine(drawingContext, line, _defaultCellWidth);
				}
				bool isMergeCommit = decoratedRevision.GetParents().Length > 1;
				bool isCollapsed = decoratedRevision.IsCollapsed;
				DrawCommitPoint(drawingContext, decoratedRevision.GraphInfo, _defaultCellWidth, isMergeCommit, isCollapsed);
				drawingContext.Pop();
			}
			base.OnRender(drawingContext);
		}

		private void AddColumnGuideline(GuidelineSet guidelines, int column)
		{
			guidelines.GuidelinesX.Add(_defaultCellWidth * (double)column);
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
						streamGeometryContext.BeginFigure(point2, isFilled: false, isClosed: false);
						streamGeometryContext.BezierTo(new Point(point2.X, point3.Y - 5.0), new Point(point3.X, point2.Y + 5.0), point3, isStroked: true, isSmoothJoin: false);
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
						streamGeometryContext2.BeginFigure(point2, isFilled: false, isClosed: false);
						streamGeometryContext2.BezierTo(new Point(point2.X, point.Y), new Point(point.X + 5.0, point.Y), point, isStroked: true, isSmoothJoin: false);
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
					streamGeometryContext3.BeginFigure(point, isFilled: false, isClosed: false);
					streamGeometryContext3.BezierTo(new Point(point4.X, point.Y), new Point(point4.X, point.Y + 5.0), point4, isStroked: true, isSmoothJoin: false);
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
					streamGeometryContext.BeginFigure(new Point(center.X - _chevronSize * 0.5, center.Y - _chevronSize), isFilled: false, isClosed: false);
					streamGeometryContext.LineTo(new Point(center.X + _chevronSize * 0.5, center.Y), isStroked: true, isSmoothJoin: false);
					streamGeometryContext.LineTo(new Point(center.X - _chevronSize * 0.5, center.Y + _chevronSize), isStroked: true, isSmoothJoin: false);
				}
				else
				{
					streamGeometryContext.BeginFigure(new Point(center.X - _chevronSize, center.Y - _chevronSize * 0.5), isFilled: false, isClosed: false);
					streamGeometryContext.LineTo(new Point(center.X, center.Y + _chevronSize * 0.5), isStroked: true, isSmoothJoin: false);
					streamGeometryContext.LineTo(new Point(center.X + _chevronSize, center.Y - _chevronSize * 0.5), isStroked: true, isSmoothJoin: false);
				}
			}
			streamGeometry.Freeze();
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
					double horizontalOffset = Mouse.GetPosition(this).X + 5.0;
					_popup = CreatePopup(parent, horizontalOffset, valueOrDefault);
					_popup.IsOpen = true;
				}
			}
		}

		private void ClosePopup(bool hardClose = false)
		{
			if (_popup != null && _popup.IsOpen && (!_popup.IsMouseOver || hardClose))
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
			popup.StaysOpen = true;
			popup.AllowsTransparency = true;
			popup.PopupAnimation = PopupAnimation.Fade;
			popup.PlacementTarget = this;
			RevisionGraphTooltipUserControl revisionGraphTooltipUserControl = new RevisionGraphTooltipUserControl(repositoryUserControl, sha);
			revisionGraphTooltipUserControl.HeightChanged += delegate(object s, EventArgs<double> e)
			{
				double value = e.Value;
				popup.VerticalOffset = 0.0 - value / 2.0 - 10.0;
			};
			VisualTreeAttachmentHelper.TrySetPopupChild(popup, revisionGraphTooltipUserControl, GetType().Name + ".Popup");
			popup.MouseLeave += delegate
			{
				_closePopupTimer.Start();
			};
			return popup;
		}
	}
}
