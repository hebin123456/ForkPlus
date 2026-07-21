namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 DropPlaceAdorner（spike 空类）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/DropPlaceAdorner.cs（49 行）：
    //   - WPF DropPlaceAdorner : Adorner
    //   - 构造接收 UIElement adornedElement + DropPosition position + ListViewItem listViewItem
    //   - _pen = new Pen(Theme.AccentBrush, 2.0)（放置位置指示线画笔）
    //   - IsHitTestVisible = false（指示线不响应鼠标）
    //   - OnRender(DrawingContext)：
    //     DropPosition.Top → DrawLine(_pen, rect.TopLeft, rect.TopRight)（上边线）
    //     DropPosition.Bottom → DrawLine(_pen, rect.BottomLeft, rect.BottomRight)（下边线）
    //     DropPosition.Over → _listViewItem.Background = Theme.RevisionList.ItemSelectedInactiveBackgroundBrush
    //   - ClearBackground() → _listViewItem.Background = Theme.RevisionList.ItemBackgroundBrush
    //
    // Avalonia 版差异（spike 简化策略，task spec：省略，保留空类）：
    //   1. WPF Adorner 基类 → Avalonia AdornerLayer（API 差异大，spike 省略）
    //   2. WPF Theme.AccentBrush → Avalonia DynamicResource（spike 省略）
    //   3. WPF OnRender(DrawingContext) → Avalonia Render(DrawingContext)
    //      spike 省略（无 AdornerLayer 装载点）
    //   4. WPF _listViewItem.Background = ... → Avalonia ListBoxItem.Background
    //      spike 省略（无视觉反馈）
    //   5. spike 保留空类 + 注释，供后续 Phase 接入 AdornerLayer 时参考
    //
    // spike 简化（task spec：省略，保留空类）：
    //   - 空类（无逻辑）
    //   - ClearBackground() 占位方法（无视觉反馈）
    public class DropPlaceAdorner
    {
        // 对照 WPF: public DropPlaceAdorner(UIElement adornedElement, DropPosition position, ListViewItem listViewItem)
        public DropPlaceAdorner()
        {
            // spike: 空实现（Avalonia AdornerLayer API 差异大，省略放置位置视觉反馈）
        }

        // 对照 WPF: internal void ClearBackground()
        // spike: 占位，无视觉反馈（_listViewItem.Background 重置省略）
        public void ClearBackground()
        {
            // spike: 空实现
        }
    }
}
