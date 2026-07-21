using Avalonia;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 DragAndDropListViewAdorner（spike 空类）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/DragAndDropListViewAdorner.cs（52 行）：
    //   - WPF DragAndDropListViewAdorner : Adorner
    //   - 构造接收 UIElement adornedElement + ListBoxItem[] listBoxItems + Point position
    //   - _visualBrushes = listBoxItems.Map(x => new VisualBrush(x) { Opacity = 0.4 })
    //   - _visualBrushYOffset = listBoxItems.FirstItem()?.ActualHeight ?? 0.0
    //   - UpdatePosition(Point position) → NewPosition = position + InvalidateVisual()
    //   - OnRender(DrawingContext) → DrawRectangle(brush, null, rect) 多个 visualBrush 偏移绘制
    //   - IsHitTestVisible = false（拖拽预览不响应鼠标）
    //
    // Avalonia 版差异（spike 简化策略，task spec：省略 Avalonia AdornerLayer API 差异大，保留空类 + 注释）：
    //   1. WPF Adorner 基类 → Avalonia AdornerLayer（API 差异大，spike 省略）
    //   2. WPF VisualBrush（拖拽项半透明预览）→ Avalonia 无直接等价（spike 省略视觉反馈）
    //   3. WPF OnRender(DrawingContext) → Avalonia Render(DrawingContext)
    //      spike 省略（无 AdornerLayer 装载点）
    //   4. spike 保留空类 + 注释，供后续 Phase 接入 AdornerLayer 时参考
    //
    // spike 简化（task spec：省略，保留空类）：
    //   - 空类（无逻辑）
    //   - UpdatePosition(Point) 占位方法（无视觉反馈）
    public class DragAndDropListViewAdorner
    {
        // 对照 WPF: public DragAndDropListViewAdorner(UIElement adornedElement, ListBoxItem[] listBoxItems, Point position)
        public DragAndDropListViewAdorner()
        {
            // spike: 空实现（Avalonia AdornerLayer API 差异大，省略拖拽视觉反馈）
        }

        // 对照 WPF: public void UpdatePosition(Point position)
        // spike: 占位，无视觉反馈（NewPosition = position + InvalidateVisual 省略）
        public void UpdatePosition(Point position)
        {
            // spike: 空实现
        }
    }
}
