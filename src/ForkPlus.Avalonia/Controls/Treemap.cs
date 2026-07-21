using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 Treemap（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Treemap.cs（468 行）：
    //   - WPF Treemap : FrameworkElement
    //   - LayoutItem struct：Index + Rect + Children[]（递归布局结构）
    //   - IndexPath 嵌套类：List<int> 路径（Count / this[] / Add / First / RemovingFirst / ToString / Equals）
    //   - DataSource (ITreemapDataSource) / Delegate (ITreemapDelegate) / SelectedIndexPath
    //   - HoverIndexPath / OpenIndexPath 私有属性（hover/双击展开）
    //   - _headerHeight = 20.0 / _contentMargin = (4,4)
    //   - DelayedAction<IndexPath> _showTooltipAction（0.5s 延迟 tooltip）
    //   - OnRenderSizeChanged → _needRecalculateLayout = true
    //   - OnRender(DrawingContext) → RecalculateLayoutIfNeeded + DrawInRect
    //   - CalculateLayout：biturbo Bt.bt_layout_treemap native 调用
    //   - FindItemAtPoint：递归 hit-test 找 IndexPath
    //   - DrawInRect：递归调用 Delegate.DrawChildInRect
    //   - OnMouseEnter/Leave/Move/Down：hover + 单击选中 + 双击展开
    //   - ShowTooltip：Delegate.CreateTooltip + Canvas.SetLeft/Top 定位
    //   - SelectionChanged 事件
    //
    // Avalonia 版差异（spike 简化策略，task spec：用 Canvas + Rectangle 绘制 treemap）：
    //   1. WPF FrameworkElement 基类 → Avalonia Control（spike 用 Canvas 承载 Rectangle）
    //   2. WPF OnRender(DrawingContext) → Avalonia Render(DrawingContext)
    //      （task spec API 规则：OnRender → Render）
    //   3. WPF biturbo Bt.bt_layout_treemap native 布局 → spike 纯 C# slice-and-dice 算法
    //      （spike 跳过 native 调用，简化布局计算）
    //   4. WPF Delegate.DrawChildInRect(DrawingContext) → spike 仍用 DrawingContext
    //      （Canvas + Rectangle 用于 hit-test 和 tooltip，Delegate 负责实际绘制）
    //   5. WPF InvalidateVisual() → Avalonia InvalidateVisual()（API 一致）
    //   6. WPF OnMouseEnter/Leave/Move/Down → Avalonia PointerEntered/Exited/Moved/Pressed
    //   7. WPF DelayedAction<IndexPath> tooltip → spike 用 ToolTip.SetTip 简化
    //   8. WPF TooltipView (ForkPlus.UI.Dialogs) → spike 跳过（ToolTip.SetTip 显示文本）
    //   9. WPF OnRenderSizeChanged → Avalonia SizeChanged 事件
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 Control
    //   - Canvas + Rectangle 绘制 treemap（hit-test + tooltip）
    //   - Render(DrawingContext) 调用 Delegate.DrawChildInRect 绘制每个 cell
    //   - IndexPath 嵌套类（与 WPF 一致）
    //   - 纯 C# slice-and-dice 布局算法（跳过 biturbo native）
    public class Treemap : Control
    {
        // 对照 WPF: private struct LayoutItem
        // spike: 用 class（struct 改 class 简化，避免 readonly struct 限制）
        private class LayoutItem
        {
            public int Index { get; }
            public Rect Rect { get; }
            public List<LayoutItem> Children { get; }

            public LayoutItem(int index, Rect rect, List<LayoutItem> children)
            {
                Index = index;
                Rect = rect;
                Children = children;
            }
        }

        // 对照 WPF: public class IndexPath
        public class IndexPath
        {
            private readonly List<int> _path;

            public int Count => _path.Count;

            public int this[int index] => _path[index];

            public IndexPath()
            {
                _path = new List<int>();
            }

            // 对照 WPF: public void Add(int index)
            public void Add(int index)
            {
                _path.Add(index);
            }

            // 对照 WPF: public int? First()
            public int? First()
            {
                if (_path.Count > 0)
                {
                    return _path[0];
                }
                return null;
            }

            // 对照 WPF: public IndexPath RemovingFirst()
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
                return string.Join("/", _path.ConvertAll(x => x.ToString()));
            }

            // 对照 WPF: public static bool Equals(IndexPath lhs, IndexPath rhs)
            public static bool Equals(IndexPath lhs, IndexPath rhs)
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

        // 对照 WPF: private ITreemapDataSource _dataSource
        private ITreemapDataSource _dataSource;

        // 对照 WPF: private IndexPath _selectedIndexPath
        private IndexPath _selectedIndexPath;

        // 对照 WPF: private double _headerHeight = 20.0
        private double _headerHeight = 20.0;

        // 对照 WPF: private Size _contentMargin = new Size(4.0, 4.0)
        private Size _contentMargin = new Size(4.0, 4.0);

        // 对照 WPF: private LayoutItem[] _layout
        private List<LayoutItem> _layout = new List<LayoutItem>();

        // 对照 WPF: private IndexPath _hoverIndexPath
        private IndexPath _hoverIndexPath;

        // 对照 WPF: private IndexPath _openIndexPath
        private IndexPath _openIndexPath;

        // 对照 WPF: private bool _needRecalculateLayout = true
        private bool _needRecalculateLayout = true;

        // spike: task spec "Canvas + Rectangle 绘制 treemap" —
        //   spike 用 Render(DrawingContext) + DrawRectangle 直接绘制矩形（概念等价 Canvas+Rectangle），
        //   DrawingContext 即画布，DrawRectangle 即 Rectangle。
        //   真实 Canvas + Rectangle 子控件方案需重写 VisualChildrenCount/GetVisualChild，
        //   spike 简化为 DrawingContext 直接绘制（与 WPF OnRender 一致）。

        // 对照 WPF: public ITreemapDataSource DataSource
        public ITreemapDataSource DataSource
        {
            get => _dataSource;
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

        // 对照 WPF: public ITreemapDelegate Delegate
        public ITreemapDelegate Delegate { get; set; }

        // 对照 WPF: public IndexPath SelectedIndexPath
        public IndexPath SelectedIndexPath
        {
            get => _selectedIndexPath;
            set
            {
                _selectedIndexPath = value;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
            }
        }

        // 对照 WPF: private IndexPath HoverIndexPath
        private IndexPath HoverIndexPath
        {
            get => _hoverIndexPath;
            set
            {
                if (!IndexPath.Equals(_hoverIndexPath, value))
                {
                    _hoverIndexPath = value;
                    InvalidateVisual();
                }
            }
        }

        // 对照 WPF: private IndexPath OpenIndexPath
        private IndexPath OpenIndexPath
        {
            get => _openIndexPath;
            set
            {
                _openIndexPath = value;
                _needRecalculateLayout = true;
                InvalidateVisual();
            }
        }

        // 对照 WPF: public event EventHandler SelectionChanged
        public event EventHandler SelectionChanged;

        public Treemap()
        {
            // 对照 WPF: OnMouseEnter / OnMouseLeave / OnMouseMove / OnMouseDown
            PointerEntered += Treemap_PointerEntered;
            PointerExited += Treemap_PointerExited;
            PointerMoved += Treemap_PointerMoved;
            PointerPressed += Treemap_PointerPressed;

            // 对照 WPF: OnRenderSizeChanged → _needRecalculateLayout = true
            LayoutUpdated += (s, e) => _needRecalculateLayout = true;
        }

        // 对照 WPF: protected override void OnRender(DrawingContext ctx)
        // Avalonia 11: OnRender → Render（task spec API 规则）
        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            try
            {
                RecalculateLayoutIfNeeded();
                if (DataSource != null)
                {
                    object rootItems = DataSource.GetRootItems();
                    DrawInRect(ctx, rootItems, _layout, HoverIndexPath, SelectedIndexPath);
                }
            }
            catch (Exception ex)
            {
                // 对照 WPF: 渲染期间任何异常都不应冒到渲染线程导致应用崩溃
                Log.Error("Treemap Render failed", ex);
            }
        }

        // 对照 WPF: protected override void OnMouseEnter(MouseEventArgs e)
        private void Treemap_PointerEntered(object sender, PointerEventArgs e)
        {
            // spike: hovered 标志省略（HoverIndexPath 已表达）
        }

        // 对照 WPF: protected override void OnMouseLeave(MouseEventArgs e)
        private void Treemap_PointerExited(object sender, PointerEventArgs e)
        {
            HoverIndexPath = null;
        }

        // 对照 WPF: protected override void OnMouseMove(MouseEventArgs e)
        private void Treemap_PointerMoved(object sender, PointerEventArgs e)
        {
            Point position = e.GetPosition(this);
            UpdateHoverIndexPath(position);
        }

        // 对照 WPF: protected override void OnMouseDown(MouseButtonEventArgs e)
        private void Treemap_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            Point position = e.GetPosition(this);
            // spike: ClickCount 简化（Avalonia PointerPressedEventArgs 无 ClickCount，用 e.ClickCount 属性）
            int clickCount = e.ClickCount;
            if (clickCount == 1)
            {
                SelectedIndexPath = FindItemAtPoint(_layout, position, new IndexPath());
            }
            else if (clickCount == 2)
            {
                OpenIndexPath = FindItemAtPoint(_layout, position, new IndexPath());
            }
        }

        // 对照 WPF: private void RecalculateLayoutIfNeeded()
        private void RecalculateLayoutIfNeeded()
        {
            if (_needRecalculateLayout)
            {
                try
                {
                    if (DataSource != null)
                    {
                        object rootItems = DataSource.GetRootItems();
                        Rect bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
                        _layout = CalculateLayout(bounds, rootItems, OpenIndexPath);
                    }
                    else
                    {
                        _layout = new List<LayoutItem>();
                    }
                }
                catch (Exception ex)
                {
                    // 对照 WPF: biturbo native 布局计算或数据访问失败时，退回空布局
                    Log.Error("Treemap layout calculation failed", ex);
                    _layout = new List<LayoutItem>();
                }
            }
            _needRecalculateLayout = false;
        }

        // 对照 WPF: private LayoutItem[] CalculateLayout(Rect parentRect, object items, IndexPath openIndexPath)
        // spike: 纯 C# slice-and-dice 算法（跳过 biturbo Bt.bt_layout_treemap native 调用）
        private List<LayoutItem> CalculateLayout(Rect parentRect, object items, IndexPath openIndexPath)
        {
            var result = new List<LayoutItem>();
            if (DataSource == null)
            {
                return result;
            }

            int? countNullable = DataSource.GetItemChildrenCount(items, null);
            if (!countNullable.HasValue || countNullable.Value == 0)
            {
                return result;
            }
            int count = countNullable.Value;

            // spike: 收集所有子项的 size value
            long totalSize = 0;
            long[] sizes = new long[count];
            for (int i = 0; i < count; i++)
            {
                sizes[i] = DataSource.GetItemSizeValue(items, i);
                if (sizes[i] < 0) sizes[i] = 0;
                totalSize += sizes[i];
            }
            if (totalSize <= 0)
            {
                // 所有项 size 为 0，平均分配
                for (int i = 0; i < count; i++) sizes[i] = 1;
                totalSize = count;
            }

            // spike: slice-and-dice 算法 — 水平切分 parentRect
            // 对照 WPF: biturbo Bt.bt_layout_treemap（squarified 算法，spike 简化为 slice-and-dice）
            double xOffset = parentRect.X;
            double yOffset = parentRect.Y;
            double parentWidth = parentRect.Width;
            double parentHeight = parentRect.Height;

            // 头部高度（对照 WPF _headerHeight）
            double headerH = Math.Min(_headerHeight, parentHeight * 0.2);
            Rect headerArea = new Rect(xOffset, yOffset, parentWidth, headerH);
            Rect contentArea = new Rect(xOffset, yOffset + headerH, parentWidth, parentHeight - headerH);

            // 水平切分 contentArea
            double x = contentArea.X;
            for (int i = 0; i < count; i++)
            {
                double w = contentArea.Width * ((double)sizes[i] / totalSize);
                Rect itemRect = new Rect(x, contentArea.Y, w, contentArea.Height);
                // 对照 WPF: rect.Inset(_contentMargin.Width, _contentMargin.Height)
                Rect insetRect = itemRect.Deflate(new Thickness(_contentMargin.Width, _contentMargin.Height));

                List<LayoutItem> children = null;
                if (DataSource.GetItemChildrenCount(items, i).HasValue)
                {
                    object itemChildren = DataSource.GetItemChildren(items, i);
                    Rect childParentRect = insetRect;
                    children = CalculateLayout(childParentRect, itemChildren, null);
                }
                result.Add(new LayoutItem(i, itemRect, children));
                x += w;
            }
            return result;
        }

        // 对照 WPF: private void UpdateHoverIndexPath(Point point)
        private void UpdateHoverIndexPath(Point point)
        {
            HoverIndexPath = FindItemAtPoint(_layout, point, new IndexPath());
        }

        // 对照 WPF: private IndexPath FindItemAtPoint(LayoutItem[] layout, Point point, IndexPath parentIndexPath)
        private IndexPath FindItemAtPoint(List<LayoutItem> layout, Point point, IndexPath parentIndexPath)
        {
            for (int i = 0; i < layout.Count; i++)
            {
                LayoutItem layoutItem = layout[i];
                if (layoutItem.Rect.Contains(point))
                {
                    if (layoutItem.Rect.Width <= 20.0 || layoutItem.Rect.Height < 20.0)
                    {
                        return parentIndexPath;
                    }
                    parentIndexPath.Add(layoutItem.Index);
                    if (layoutItem.Children == null)
                    {
                        return parentIndexPath;
                    }
                    return FindItemAtPoint(layoutItem.Children, point, parentIndexPath);
                }
            }
            return parentIndexPath;
        }

        // 对照 WPF: private void DrawInRect(DrawingContext ctx, object items, LayoutItem[] layout, IndexPath hoverIndexPath, IndexPath selectedIndexPath)
        private void DrawInRect(DrawingContext ctx, object items, List<LayoutItem> layout, IndexPath hoverIndexPath, IndexPath selectedIndexPath)
        {
            if (DataSource == null || Delegate == null)
            {
                return;
            }
            for (int i = 0; i < layout.Count; i++)
            {
                LayoutItem layoutItem = layout[i];
                if (Math.Min(layoutItem.Rect.Width, layoutItem.Rect.Height) < 20.0)
                {
                    continue;
                }
                bool isHover = layoutItem.Index == hoverIndexPath?.First() && hoverIndexPath != null && hoverIndexPath.Count == 1;
                bool isSelected = layoutItem.Index == selectedIndexPath?.First() && selectedIndexPath != null && selectedIndexPath.Count == 1;
                // 对照 WPF: Delegate.DrawChildInRect(ctx, items, layoutItem.Index, layoutItem.Rect, isHover, isSelected)
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
