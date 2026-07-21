using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 CustomAdorner（spike 空类）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/CustomAdorner.cs（88 行）：
    //   - WPF CustomAdorner : Adorner
    //   - 构造接收 UIElement adornernedElement + bool centeredHorizontally
    //   - Child 属性：FrameworkElement（AddLogicalChild + AddVisualChild + RemoveVisualChild）
    //   - VisualChildrenCount => (Child != null) ? 1 : 0
    //   - GetVisualChild(int index) => Child
    //   - MeasureOverride：Child.Measure + 最小宽度 40.0
    //   - ArrangeOverride：centered 时 x = -finalSize.Width/2
    //   - VisualTreeAttachmentHelper.PrepareForNewParent / TryAddChild
    //   - 用于 MainWindow 的 Loading 覆盖层等场景
    //
    // Avalonia 版差异（spike 简化策略，task spec：省略，保留空类）：
    //   1. WPF Adorner 基类 → Avalonia AdornerLayer（API 差异大，spike 省略）
    //   2. WPF AddLogicalChild / AddVisualChild / RemoveVisualChild → Avalonia 无直接等价
    //   3. WPF VisualTreeAttachmentHelper.PrepareForNewParent → Avalonia 无等价
    //   4. WPF MeasureOverride / ArrangeOverride → Avalonia MeasureOverride / ArrangeOverride
    //      spike 省略（无 AdornerLayer 装载点）
    //   5. spike 保留空类 + 注释，供后续 Phase 接入 AdornerLayer 时参考
    //
    // spike 简化（task spec：省略，保留空类）：
    //   - 空类（无逻辑）
    //   - Child 属性占位（无视觉承载）
    public class CustomAdorner
    {
        // 对照 WPF: public FrameworkElement Child { get; set; }
        // spike: 占位，无视觉承载（AddLogicalChild / AddVisualChild 省略）
        public Control Child { get; set; }

        // 对照 WPF: public CustomAdorner(UIElement adornernedElement, bool centeredHorizontally = false)
        public CustomAdorner()
        {
            // spike: 空实现（Avalonia AdornerLayer API 差异大，省略）
        }
    }
}
