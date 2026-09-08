using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class GraphCellView : global::Avalonia.Controls.Control
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

		[Null]
		private global::Avalonia.Controls.Panel _popupHost;

		[Null]
		private global::Avalonia.Controls.Control _popupContent;

		[Null]
		private global::Avalonia.Controls.TopLevel _popupPointerMoveRoot;

		// 弹窗打开期间订阅顶层 PointerMoved（只有鼠标真实移动才触发；滚动/布局变化导致的
		// 命中测试重算不会触发）——鼠标移出单元格且不在弹窗上时才启动关闭定时器。
		private void OnPopupRootPointerMoved(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			if (_popup == null || !_popup.IsOpen)
			{
				return;
			}
			bool overContent = _popupContent != null && _popupContent.IsPointerOver;
			if (IsPointerOver || overContent)
			{
				_closePopupTimer.Stop();
			}
			else
			{
				_closePopupTimer.Start();
			}
		}

		private void UnhookPopupPointerMove()
		{
			if (_popupPointerMoveRoot != null)
			{
				_popupPointerMoveRoot.RemoveHandler(global::Avalonia.Input.InputElement.PointerMovedEvent, (EventHandler<global::Avalonia.Input.PointerEventArgs>)OnPopupRootPointerMoved);
				_popupPointerMoveRoot = null;
			}
		}

		private Sha? _activeMergePointSha;

		// WPF Mouse.GetPosition(this) 的替代：WpfCompat Mouse shim 恒返回 (0,0)。
		// 在指针事件中记录真实 X（供命中测试列计算使用）。
		private double _lastPointerX;

		// Migration note：字段类型收紧为 StyledProperty<T>（XAML 编译器要求 typed property）。
                public static readonly global::Avalonia.StyledProperty<double> CellHeightProperty;

                public static readonly global::Avalonia.StyledProperty<bool> ShowGraphToolTipProperty;

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
			CellHeightProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<GraphCellView, double>("CellHeight", _defaultCellHeight);
			ShowGraphToolTipProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<GraphCellView, bool>("ShowGraphToolTip", true);
			Pen[] branchPens = _branchPens;
			for (int i = 0; i < branchPens.Length; i++)
			{
			}
		}

		public GraphCellView()
		{
			base.UseLayoutRounding= true;
			if (ShowGraphToolTip)
			{
				_showPopupTimer.Interval = TimeSpan.FromMilliseconds(600.0);
				_closePopupTimer.Interval = TimeSpan.FromMilliseconds(200.0);
				_showPopupTimer.Tick += _showPopupTimer_Tick;
				_closePopupTimer.Tick += _closePopupTimer_Tick;
			}
		}

		// 修复（2026-09-05，"轨道图刷出来一片白色，点击才显示"）：
		// 虚拟化列表回收复用时，容器短暂 detach → DataContext 变化 → InvalidateVisual
		// 只打脏标记但不真正渲染（控件不在渲染路径上）。重新 attach 后 Avalonia
		// 只做 Measure/Arrange（Bounds 没变），不会自动触发 Render → 控件透出
		// 背景色（"白屏"）。点击行触发 ListBoxItem 重绘才把子控件一起画出来。
		// 修复：AttachedToVisualTree 时强制 InvalidateVisual，确保重新挂树后立即渲染。
		protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			InvalidateMeasure();
			InvalidateVisual();
		}

		protected override void OnPointerEntered(global::Avalonia.Input.PointerEventArgs e)
		{
			// Migration note：WPF PointerEventArgs.LeftButton != MouseButtonState.Pressed → GetCurrentPoint().Properties.IsLeftButtonPressed
			if (ShowGraphToolTip && base.DataContext is DecoratedRevision decoratedRevision && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				base.OnPointerEntered(e);
				_lastPointerX = e.GetPosition(this).X;
				if (decoratedRevision.GetParents().Length > 1)
				{
					_activeMergePointSha = decoratedRevision.Sha;
					_showPopupTimer.Start();
					_closePopupTimer.Stop();
				}
			}
		}

		protected override void OnPointerExited(global::Avalonia.Input.PointerEventArgs e)
		{
			base.OnPointerExited(e);
			IsMouseOver = false;
			if (ShowGraphToolTip && base.DataContext is DecoratedRevision decoratedRevision && decoratedRevision.GetParents().Length > 1)
			{
				_activeMergePointSha = null;
				_showPopupTimer.Stop();
				// 弹窗已打开时不在这里启动关闭定时器：滚动/布局变化会对静止鼠标重新命中测试，
				// 产生假的 PointerExited。关闭时机改由顶层 PointerMoved 处理器决定（见 CreatePopup）。
				if (_popup == null || !_popup.IsOpen)
				{
					_closePopupTimer.Start();
				}
			}
		}

		protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
		{
			base.OnPointerMoved(e);
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				_lastPointerX = e.GetPosition(this).X;
				int num = (int)((_lastPointerX + 5.0) / _defaultCellWidth);
				IsMouseOver = num == decoratedRevision.GraphInfo.CurrentCommitColumn;
			}
		}

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			// Migration note：WPF PointerPressedEventArgs.ChangedButton == MouseButton.Left → GetCurrentPoint().Properties.IsLeftButtonPressed
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

		// Migration note：WPF 版在 OnRender 里直接设 this.Width = cellWidth * lines.Length（WPF 允许渲染后
		// 触发下一轮流排；Avalonia 渲染期使布局失效直接抛 InvalidOperationException
		// "Visual was invalidated during the render pass"，迁移期实证）。
		// 正确做法：MeasureOverride 按数据源返回期望尺寸（Auto 列/StackPanel 布局取该值），
		// Render 只绘制；DataContext 变化（容器回收复用换绑）时手动失效量测。
		protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize)
		{
			double num = _defaultCellWidth;
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				num = _defaultCellWidth * (double)decoratedRevision.GraphInfo.Lines.Length;
			}
			return new global::Avalonia.Size(num, CellHeight);
		}

		protected override void OnDataContextChanged(EventArgs e)
		{
			base.OnDataContextChanged(e);
			InvalidateMeasure();
			InvalidateVisual();
		}

		public override void Render(DrawingContext drawingContext)
		{
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				// WPF 版在此构造 GuidelineSet 并 PushGuidelineSet 做像素对齐（列坐标均为
				// cellWidth 整数倍，1px 线条需对齐到设备像素中点）。Avalonia 无 GuidelineSet，
				// Migration note：如需恢复像素级锐利，可在 DrawLine/DrawCommitPoint 内对坐标做
				// Math.Floor(x)+0.5 半像素偏移；当前先按原始坐标绘制（视觉上可能有轻微模糊）。
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

		// WPF GuidelineSet 辅助已随 PushGuidelineSet 一并移除（见 Render 内注释）

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
						streamGeometryContext.BeginFigure(point2,false);
						streamGeometryContext.CubicBezierTo(new Point(point2.X, point3.Y - 5.0),new Point(point3.X, point2.Y + 5.0),point3,true);
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
						streamGeometryContext2.BeginFigure(point2,false);
						streamGeometryContext2.CubicBezierTo(new Point(point2.X, point.Y),new Point(point.X + 5.0, point.Y),point,true);
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
					streamGeometryContext3.BeginFigure(point,false);
					streamGeometryContext3.CubicBezierTo(new Point(point4.X, point.Y),new Point(point4.X, point.Y + 5.0),point4,true);
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
			drawingContext.DrawEllipse(global::ForkPlus.UI.Theme.RevisionList.ItemBackgroundBrush, pen2, center, _commitMergePointRadius, _commitMergePointRadius);
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				if (isCollapsed)
				{
					streamGeometryContext.BeginFigure(new Point(center.X - _chevronSize * 0.5, center.Y - _chevronSize),false);
					streamGeometryContext.LineTo(new Point(center.X + _chevronSize * 0.5, center.Y),true);
					streamGeometryContext.LineTo(new Point(center.X - _chevronSize * 0.5, center.Y + _chevronSize),true);
				}
				else
				{
					streamGeometryContext.BeginFigure(new Point(center.X - _chevronSize, center.Y - _chevronSize * 0.5),false);
					streamGeometryContext.LineTo(new Point(center.X, center.Y + _chevronSize * 0.5),true);
					streamGeometryContext.LineTo(new Point(center.X + _chevronSize, center.Y - _chevronSize * 0.5),true);
				}
			}
			drawingContext.DrawGeometry(null, pen, streamGeometry);
		}

		private void ShowPopup()
		{
			try
			{
				RepositoryUserControl parent = this.GetParent<RepositoryUserControl>();
				if (parent != null && (_popup == null || !_popup.IsOpen))
				{
					Sha? activeMergePointSha = _activeMergePointSha;
					if (activeMergePointSha.HasValue)
					{
						Sha valueOrDefault = activeMergePointSha.GetValueOrDefault();
						_popup = CreatePopup(parent, valueOrDefault);
						_popup.IsOpen = true;
					}
				}
			}
			catch (System.Exception ex)
			{
				Log.Error("GraphCellView.ShowPopup failed: " + ex);
			}
		}

		private void ClosePopup(bool hardClose = false)
		{
			// Bug 修复（弹窗悬停闪烁死循环）：Avalonia 的 Popup 不是 Visual，IsPointerOver 恒为
			// false → 原判断 !_popup.IsPointerOver 永远成立 → 弹窗一盖住轨道图单元格就触发
			// PointerExited → 关窗 → 鼠标又回到单元格 → 再开 → 死循环。改为检查弹窗内容控件
			//（真正的 Visual）的 IsPointerOver，并在内容 PointerEntered 时停止关闭定时器。
			bool pointerOverContent = (_popupContent != null && _popupContent.IsPointerOver) || IsPointerOver;
			if (_popup != null && _popup.IsOpen && (!pointerOverContent || hardClose))
			{
				_popup.IsOpen = false;
				VisualTreeAttachmentHelper.TrySetPopupChild(_popup, null, GetType().Name + ".Popup");
				DetachPopupFromHost(_popup);
				UnhookPopupPointerMove();
				_popup = null;
				_popupContent = null;
			}
		}

		private void DetachPopupFromHost(Popup popup)
		{
			if (_popupHost != null)
			{
				_popupHost.Children.Remove(popup);
				_popupHost = null;
			}
		}

		private Popup CreatePopup(RepositoryUserControl repositoryUserControl, Sha sha)
		{
			Popup popup = new Popup();
			// 弹窗定位：左上角对齐合并按钮（圆点）的右下角，不遮挡按钮。
			// 圆点圆心 = (cellWidth * CurrentCommitColumn, CellHeight/2)，半径 _commitMergePointRadius。
			// 注意：Placement=Bottom 是 xdg 语义——锚点取锚定矩形底边【中点】，弹窗水平居中过去，
			// 这就是之前"按钮跑到弹窗中心"的原因。改用 AnchorAndGravity 精确定位（同 Menu.axaml）：
			// PlacementAnchor=BottomRight 锚点取按钮矩形的右下角，PlacementGravity=BottomRight
			// 弹窗从该点向右下延伸（即弹窗左上角 = 按钮右下角 + 偏移）。
			Rect anchorRect = new Rect(_defaultCellWidth - _commitMergePointRadius, CellHeight - _commitMergePointRadius, _commitMergePointRadius * 2.0, _commitMergePointRadius * 2.0);
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				double centerX = _defaultCellWidth * (double)(int)decoratedRevision.GraphInfo.CurrentCommitColumn;
				double centerY = CellHeight / 2.0;
				anchorRect = new Rect(centerX - _commitMergePointRadius, centerY - _commitMergePointRadius, _commitMergePointRadius * 2.0, _commitMergePointRadius * 2.0);
			}
			popup.Placement = PlacementMode.AnchorAndGravity;
			popup.PlacementAnchor = global::Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.BottomRight;
			popup.PlacementGravity = global::Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.BottomRight;
			popup.PlacementRect = anchorRect;
			popup.HorizontalOffset = 4.0;
			popup.VerticalOffset = 4.0;
			popup.IsLightDismissEnabled= (!true);
			/* Migration note: AllowsTransparency 已删除 */;
			/* Migration note: PopupAnimation 已删除 */;
			popup.PlacementTarget = this;
			RevisionGraphTooltipUserControl revisionGraphTooltipUserControl = new RevisionGraphTooltipUserControl(repositoryUserControl, sha);
			VisualTreeAttachmentHelper.TrySetPopupChild(popup, revisionGraphTooltipUserControl, GetType().Name + ".Popup");
			_popupContent = revisionGraphTooltipUserControl;
			// 鼠标进入弹窗内容时取消关闭定时器（可在弹窗上自由移动/点击），离开后再启动关闭。
			revisionGraphTooltipUserControl.PointerEntered += delegate
			{
				_closePopupTimer.Stop();
			};
			revisionGraphTooltipUserControl.PointerExited += delegate
			{
				_closePopupTimer.Start();
			};
			// Bug 修复（合并节点悬浮详情弹窗空白）：孤立 Popup（不在逻辑树中）的子控件
			// DynamicResource 解析链在自身截止（BorderBrush/ListBox.Static.Background/主题全部
			// 解析为 null）→ 弹窗只剩宿主白底、无边框无内容。与 GitMmUserControl 本轮25 修复
			// 同法：把 Popup 挂进 Panel 祖先（不占布局空间），资源沿逻辑树正常解析；
			// 关闭时移除防泄漏。
			// 注意宿主选择：不能挂最近的行容器 Panel——提交列表是虚拟化的，点击/滚动触发的
			// 布局刷新会回收行容器（PlacementTarget 随之 detach → Avalonia 自动关弹窗，
			// 即"点击/滚动时弹窗突然消失"）。这里挂到 RepositoryUserControl 根 Grid
			//（稳定、不随行回收），找不到才退回最近的 Panel。
			global::Avalonia.Controls.Panel fallbackPanel = null;
			global::Avalonia.Visual ancestor = this;
			while ((ancestor = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(ancestor)) != null)
			{
				if (fallbackPanel == null && ancestor is global::Avalonia.Controls.Panel nearPanel)
				{
					fallbackPanel = nearPanel;
				}
				if (ancestor is RepositoryUserControl)
				{
					foreach (global::Avalonia.Visual child in global::Avalonia.VisualTree.VisualExtensions.GetVisualChildren(ancestor))
					{
						if (child is global::Avalonia.Controls.Panel rootPanel)
						{
							_popupHost = rootPanel;
							break;
						}
					}
					break;
				}
			}
			if (_popupHost == null)
			{
				_popupHost = fallbackPanel;
			}
			if (_popupHost != null)
			{
				_popupHost.Children.Add(popup);
				Popup popupRef = popup;
				popup.Closed += delegate
				{
					DetachPopupFromHost(popupRef);
					UnhookPopupPointerMove();
				};
			}
			// 弹窗打开期间：滚动/点击/布局变化不再直接关窗（Avalonia 会对静止鼠标重新命中测试，
			// 滚动时单元格会收到假 PointerExited；WPF 只在鼠标真实移动时改变悬停）。
			// 改为监听顶层 PointerMoved（仅真实鼠标移动触发）决定关闭时机。
			_popupPointerMoveRoot = global::Avalonia.Controls.TopLevel.GetTopLevel(this);
			if (_popupPointerMoveRoot != null)
			{
				_popupPointerMoveRoot.AddHandler(global::Avalonia.Input.InputElement.PointerMovedEvent,
					(EventHandler<global::Avalonia.Input.PointerEventArgs>)OnPopupRootPointerMoved,
					global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble,
					true);
			}
			popup.PointerExited += delegate
			{
				_closePopupTimer.Start();
			};
			return popup;
		}
	}
}
