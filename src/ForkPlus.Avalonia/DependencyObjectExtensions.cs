using Avalonia;
using Avalonia.VisualTree;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/DependencyObjectExtensions.cs（18 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - GetParent<T>(this DependencyObject _this) where T : DependencyObject
    //     → 向上遍历 VisualTreeHelper.GetParent 直到找到 T 类型节点
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. DependencyObject → Avalonia.AvaloniaObject（spike 规范）
    //   2. VisualTreeHelper.GetParent → Avalonia.VisualTree.IVisual.GetVisualParent()
    //      （Avalonia 11 用 IVisual 接口的 GetVisualParent 扩展方法替代 VisualTreeHelper 静态类）
    //   3. AvaloniaObject 本身不一定实现 IVisual（只有 Visual 子类实现），
    //      spike 版先 cast 为 IVisual，若不可转换则返回 default(T)
    //   4. [Null] 特性移除（NullAttribute 在 ForkPlus.Core 是 internal，跨工程不可访问）
    public static class DependencyObjectExtensions
    {
        public static T GetParent<T>(this AvaloniaObject _this) where T : AvaloniaObject
        {
            // spike: Avalonia 11 用 Visual 类替代 IVisual 接口，GetVisualParent() 是 Visual 的扩展方法
            Visual current = _this as Visual;
            while (current != null && !(current is T))
            {
                current = current.GetVisualParent();
            }
            return current is T result ? result : null;
        }
    }
}
