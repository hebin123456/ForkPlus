using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ITreemapDelegate（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ITreemapDelegate.cs（16 行）：
    //   - WPF ITreemapDelegate 接口（Treemap 绘制委托抽象）
    //   - GetItemTitle(object array, int index) → string（节点标题，用于 tooltip）
    //   - DrawChildInRect(DrawingContext ctx, object items, int index, Rect rect, bool isHover, bool isSelected)
    //     （在指定 Rect 内绘制单个节点：背景色 + 标题文本）
    //   - CreateTooltip(Treemap.IndexPath indexPath) → TooltipView（悬停 tooltip 视图）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Media.DrawingContext → Avalonia.Media.DrawingContext
    //   2. WPF System.Windows.Rect → Avalonia.Rect
    //   3. WPF TooltipView（ForkPlus.UI.Dialogs）→ spike 用 Avalonia.Controls.Control 占位
    //      （TooltipView 是 WPF 工程 ForkPlus.UI.Dialogs 类型，Avalonia 工程不可访问）
    //   4. spike 跳过 CreateTooltip（返回 null），由 Treemap 自身用 ToolTip.SetTip 显示文本
    //
    // spike 简化（task spec：接口）：
    //   - GetItemTitle / DrawChildInRect 签名与 WPF 一致（DrawingContext / Rect 改 Avalonia）
    //   - CreateTooltip 返回 Control 占位（spike 跳过自定义 tooltip 视图）
    public interface ITreemapDelegate
    {
        // 对照 WPF: string GetItemTitle(object array, int index)
        string GetItemTitle(object array, int index);

        // 对照 WPF: void DrawChildInRect(DrawingContext ctx, object items, int index, Rect rect, bool isHover, bool isSelected)
        // spike: DrawingContext / Rect 改 Avalonia.Media.DrawingContext / Avalonia.Rect
        void DrawChildInRect(DrawingContext ctx, object items, int index, Rect rect, bool isHover, bool isSelected);

        // 对照 WPF: TooltipView CreateTooltip(Treemap.IndexPath indexPath)
        // spike: 返回 Avalonia.Controls.Control 占位（TooltipView 在 WPF 工程，不可访问）
        Control CreateTooltip(Treemap.IndexPath indexPath);
    }
}
