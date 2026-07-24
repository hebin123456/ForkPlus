// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Controls + using Avalonia.Input + using Avalonia.Media
// - FrameworkElement → Avalonia.Controls.Control
// - OnRender(DrawingContext) → Render(DrawingContext)（Avalonia 渲染方法名）
// - base.RenderSize → Bounds.Size
// - OnRenderSizeChanged(SizeChangedInfo) → ArrangeOverride 检测尺寸变化（Avalonia 无 OnRenderSizeChanged）
// - Mouse* 事件 → Pointer* 事件（OnMouseLeave → OnPointerExited）；MouseButtonEventArgs → PointerPressedEventArgs；MouseEventArgs → PointerEventArgs
// - Point.Offset(double,double) → Point + Vector（Avalonia Point 为只读结构体）
// - DrawRectangle/DrawLine/DrawText/DrawGeometry 签名兼容，保持不变
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.Helpers;
using ForkPlus;

namespace ForkPlus.UI.Controls
{
	public class Treemap : Control
	{
		private struct LayoutItem
		{
			public int Index { get; }

			public Rect Rect { get; }

			[Null]
			public LayoutItem[] Children { get; }

			public LayoutItem(int index, Rect rect, [Null] LayoutItem[] children)
			{
				Index = index;
				Rect = rect;
				Children = children;
			}
		}

		public class IndexPath
		{
			private readonly List<int> _path;

			public int Count => _path.Count;

			public int this[int index] => _path[index];

			public IndexPath()
			{
				_path = new List<int>();
			}

			public void Add(int index)
			{
				_path.Add(index);
			}

			public int? First()
			{
				if (_path.Count > 0)
				{
					return _path[0];
				}
				return null;
			}

			public IndexPath RemovingFirst()
			{
				IndexPath indexPath = new IndexPath();
				for (int i = 1; i < _path.Count; i++)
				{
					indexPath.Add(_path[i]);
				}
				return indexPath;
			}

			public override string ToString()
			{
				return string.Join("/", _path.Map((int x) => x.ToString()));
			}

			public static bool Equals([Null] IndexPath lhs, [Null] IndexPath rhs)
			{
				if (lhs != null)
				{
					if (rhs == null)
					{
						return false;
					}
					if (lhs.Count != rhs.Count)
					{
						return false;
					}
					for (int i = 0; i < lhs.Count; i++)
					{
						if (lhs[i] != rhs[i])
						{
							return false;
						}
					}
					return true;
				}
				return rhs == null;
			}
		}

		[Null]
		private ITreemapDataSource _dataSource;

		[Null]
		private IndexPath _selectedIndexPath;

		private double _headerHeight = 20.0;

		private Size _contentMargin = new Size(4.0, 4.0);

		private LayoutItem[] _layout = new LayoutItem[0];

		private TooltipView _tooltipView;

		private DelayedAction<IndexPath> _showTooltipAction;

		[Null]
		private IndexPath _hoverIndexPath;

		[Null]
		private IndexPath _openIndexPath;

		private bool hovered;

		private bool _needRecalculateLayout = true;

		// TODO(4.5): WPF OnRenderSizeChanged(SizeChangedInfo) → Avalonia 无直接等价物。
		// 用 ArrangeOverride 检测尺寸变化，触发布局重算（替代 OnRenderSizeChanged）。
		private Size _lastArrangeSize;

		[Null]
		public ITreemapDataSource DataSource
		{
			get
			{
				return _dataSource;
			}
			set
			{
				_dataSource = value;
				OpenIndexPath = null;
				HoverIndexPath = null;
				SelectedIndexPath = null;
				_needRecalculateLayout = true;
				InvalidateVisual();
			}
		}

		[Null]
		public ITreemapDelegate Delegate { get; set; }

		[Null]
		public IndexPath SelectedIndexPath
		{
			get
			{
				return _selectedIndexPath;
			}
			set
			{
				_selectedIndexPath = value;
				this.SelectionChanged?.Invoke(this, EventArgs.Empty);
				InvalidateVisual();
			}
		}

		private Rect _bounds => new Rect(new Point(0.0, 0.0), Bounds.Size);

		private Canvas Canvas => ((base.Parent as Grid).Parent as Grid).Parent as Canvas;

		[Null]
		private IndexPath HoverIndexPath
		{
			get
			{
				return _hoverIndexPath;
			}
			set
			{
				if (!IndexPath.Equals(_hoverIndexPath, value))
				{
					_hoverIndexPath = value;
					_showTooltipAction.Cancel();
					if (_tooltipView != null)
					{
						Canvas.Children.Remove(_tooltipView);
						_tooltipView = null;
					}
					if (_hoverIndexPath != null)
					{
						_showTooltipAction.InvokeWithDelay(_hoverIndexPath);
					}
					InvalidateVisual();
				}
			}
		}

		[Null]
		private IndexPath OpenIndexPath
		{
			get
			{
				return _openIndexPath;
			}
			set
			{
				_openIndexPath = value;
				_needRecalculateLayout = true;
				InvalidateVisual();
			}
		}

		public event EventHandler SelectionChanged;

		public Treemap()
		{
			_showTooltipAction = new DelayedAction<IndexPath>(ShowTooltip, 0.5);
		}

		// TODO(4.5): WPF OnRenderSizeChanged(SizeChangedInfo) → Avalonia ArrangeOverride 检测尺寸变化。
		protected override Size ArrangeOverride(Size finalSize)
		{
			Size result = base.ArrangeOverride(finalSize);
			if (result != _lastArrangeSize)
			{
				_lastArrangeSize = result;
				_needRecalculateLayout = true;
			}
			return result;
		}

		public override void Render(DrawingContext ctx)
		{
			base.Render(ctx);
			try
			{
				RecalculateLayoutIfNeeded();
				if (DataSource != null)
				{
					Benchmarker benchmarker = new Benchmarker("DrawInRect");
					object rootItems = DataSource.GetRootItems();
					DrawInRect(ctx, rootItems, _layout, HoverIndexPath, SelectedIndexPath);
					benchmarker.LogElapsed();
				}
			}
			catch (Exception ex)
			{
				// 渲染期间任何异常（含 native biturbo 调用失败）都不应冒到 Avalonia 渲染线程导致应用崩溃。
				Log.Error("Treemap OnRender failed", ex);
			}
		}

		protected override void OnPointerEntered(PointerEventArgs e)
		{
			base.OnPointerEntered(e);
			if (!hovered)
			{
				hovered = true;
			}
		}

		protected override void OnPointerExited(PointerEventArgs e)
		{
			base.OnPointerExited(e);
			if (hovered)
			{
				hovered = false;
				HoverIndexPath = null;
			}
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			Point position = e.GetPosition(this);
			UpdateHoverIndexPath(position);
			UpdateTooltipPosition();
			base.OnPointerMoved(e);
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			Point position = e.GetPosition(this);
			// TODO(4.5): Avalonia PointerPressedEventArgs.ClickCount 标记为 Obsolete，
			// 官方建议改用 DoubleTapped 事件区分双击；当前保留 ClickCount 以维持原有业务逻辑，
			// 后续阶段改用 DoubleTapped 区分单击/双击。
			if (e.ClickCount == 1)
			{
				SelectedIndexPath = FindItemAtPoint(_layout, position, new IndexPath());
			}
			else if (e.ClickCount == 2)
			{
				OpenIndexPath = FindItemAtPoint(_layout, position, new IndexPath());
			}
			base.OnPointerPressed(e);
		}

		private void ShowTooltip(IndexPath indexPath)
		{
			if (_tooltipView != null)
			{
				Canvas.Children.Remove(_tooltipView);
				_tooltipView = null;
			}
			TooltipView tooltipView = Delegate.CreateTooltip(indexPath);
			if (!VisualTreeAttachmentHelper.TryAddChild(Canvas, tooltipView, GetType().Name + ".Tooltip"))
			{
				return;
			}
			_tooltipView = tooltipView;
			tooltipView.Show();
			UpdateTooltipPosition();
		}

		private void UpdateTooltipPosition()
		{
			if (_tooltipView != null)
			{
				// TODO(4.5): WPF Visual.PointFromScreen → Avalonia 无 Canvas.PointFromScreen 等价物，需运行时验证。
				// MouseHelper 仍为 WPF 实现，待其迁移后一并处理（PointToClient / 屏幕坐标转换）。
				Point point = Canvas.PointFromScreen(MouseHelper.GetMousePosition());
				// TODO(4.5): WPF Point.Offset(double,double) → Avalonia Point + Vector（Point 为只读结构体）。
				point = point + new Vector(10.0, 10.0);
				Canvas.SetLeft(_tooltipView, point.X);
				Canvas.SetTop(_tooltipView, point.Y);
			}
		}

		private void RecalculateLayoutIfNeeded()
		{
			if (_needRecalculateLayout)
			{
				try
				{
					if (DataSource != null)
					{
						object rootItems = DataSource.GetRootItems();
						_layout = CalculateLayout(_bounds, rootItems, OpenIndexPath);
					}
					else
					{
						_layout = new LayoutItem[0];
					}
				}
				catch (Exception ex)
				{
					// biturbo native 布局计算或数据访问失败时，退回空布局，避免崩溃。
					Log.Error("Treemap layout calculation failed", ex);
					_layout = new LayoutItem[0];
				}
			}
			_needRecalculateLayout = false;
		}

		private LayoutItem[] CalculateLayout(Rect parentRect, object items, [Null] IndexPath openIndexPath)
		{
			if (DataSource == null)
			{
				return new LayoutItem[0];
			}
			(int, Rect)[] source = new(int, Rect)[0];
			IndexPath openIndexPathTail = null;
			if (openIndexPath != null && openIndexPath.Count > 0)
			{
				int? num = openIndexPath.First();
				if (num.HasValue)
				{
					int valueOrDefault = num.GetValueOrDefault();
					openIndexPathTail = openIndexPath.RemovingFirst();
					source = new(int, Rect)[1] { (valueOrDefault, parentRect) };
				}
			}
			else
			{
				int value = DataSource.GetItemChildrenCount(items, null).Value;
				long[] values = new long[value];
				for (int i = 0; i < value; i++)
				{
					values[i] = DataSource.GetItemSizeValue(items, i);
				}
				BtRect btRect = new BtRect
				{
					x = parentRect.X,
					y = parentRect.Y,
					w = parentRect.Width,
					h = parentRect.Height
				};
				GitCommandResult<(int, Rect)[]> gitCommandResult = BtRequest.Run(() => default(BtLayoutTreemapResult), delegate(ref BtLayoutTreemapResult x)
				{
					return Bt.bt_layout_treemap(values, values.Length, btRect, ref x);
				}, delegate(ref BtLayoutTreemapResult x)
				{
					return x.Into();
				}, delegate(ref BtLayoutTreemapResult x)
				{
					Bt.bt_release_layout_treemap(ref x);
				});
			if (!gitCommandResult.Succeeded)
			{
				// biturbo native 布局计算失败（可能因仓库文件数过多、值异常或 native 内部 bug）。
			// 不抛异常——抛在 OnRender 期间会冒到 Avalonia 渲染线程导致应用整体崩溃。
				// 返回空布局，Treemap 显示空白，并记录错误日志便于诊断。
				Log.Error("bt_layout_treemap failed: " + gitCommandResult.Error.FriendlyDescription);
				return new LayoutItem[0];
			}
			source = gitCommandResult.Result;
			}
			return source.Map(delegate((int, Rect) x)
			{
				x.Item2.DivideFromTop(_headerHeight).Deconstruct(out var _, out var item2);
				Rect rect = item2;
				LayoutItem[] children = null;
				if (DataSource.GetItemChildrenCount(items, x.Item1).HasValue)
				{
					object itemChildren = DataSource.GetItemChildren(items, x.Item1);
					Rect parentRect2 = rect.Inset(_contentMargin.Width, _contentMargin.Height);
					children = CalculateLayout(parentRect2, itemChildren, openIndexPathTail);
				}
				return new LayoutItem(x.Item1, x.Item2, children);
			});
		}

		private void UpdateHoverIndexPath(Point point)
		{
			HoverIndexPath = FindItemAtPoint(_layout, point, new IndexPath());
		}

		private IndexPath FindItemAtPoint(LayoutItem[] layout, Point point, IndexPath parentIndexPath)
		{
			for (int i = 0; i < layout.Length; i++)
			{
				LayoutItem layoutItem = layout[i];
				if (layoutItem.Rect.Contains(point))
				{
					if (layoutItem.Rect.Width <= 20.0 || layoutItem.Rect.Height < 20.0)
					{
						return parentIndexPath;
					}
					parentIndexPath.Add(layoutItem.Index);
					LayoutItem[] children = layoutItem.Children;
					if (children == null)
					{
						return parentIndexPath;
					}
					return FindItemAtPoint(children, point, parentIndexPath);
				}
			}
			return parentIndexPath;
		}

		private void DrawInRect(DrawingContext ctx, object items, LayoutItem[] layout, [Null] IndexPath hoverIndexPath, [Null] IndexPath selectedIndexPath)
		{
			if (DataSource == null || Delegate == null)
			{
				return;
			}
			for (int i = 0; i < layout.Length; i++)
			{
				LayoutItem layoutItem = layout[i];
				if (Math.Min(layoutItem.Rect.Width, layoutItem.Rect.Height) < 20.0)
				{
					continue;
				}
				bool isHover = layoutItem.Index == hoverIndexPath?.First() && hoverIndexPath != null && hoverIndexPath.Count == 1;
				bool isSelected = layoutItem.Index == selectedIndexPath?.First() && selectedIndexPath != null && selectedIndexPath.Count == 1;
				Delegate.DrawChildInRect(ctx, items, layoutItem.Index, layoutItem.Rect, isHover, isSelected);
				if (DataSource.GetItemChildrenCount(items, layoutItem.Index).HasValue)
				{
					IndexPath hoverIndexPath2 = null;
					if (hoverIndexPath != null && layoutItem.Index == hoverIndexPath.First() && hoverIndexPath.Count > 1)
					{
						hoverIndexPath2 = hoverIndexPath.RemovingFirst();
					}
					IndexPath selectedIndexPath2 = null;
					if (selectedIndexPath != null && layoutItem.Index == selectedIndexPath.First() && selectedIndexPath.Count > 1)
					{
						selectedIndexPath2 = selectedIndexPath.RemovingFirst();
					}
					object itemChildren = DataSource.GetItemChildren(items, layoutItem.Index);
					DrawInRect(ctx, itemChildren, layoutItem.Children, hoverIndexPath2, selectedIndexPath2);
				}
			}
		}
	}
}
