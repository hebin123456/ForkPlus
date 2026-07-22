using Avalonia;
using Avalonia.Input;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/UIElementExtensions.cs（36 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - Show(this UIElement) → element.Visibility = Visibility.Visible
    //   - Collapse(this UIElement) → element.Visibility = Visibility.Collapsed
    //   - Hide(this UIElement) → element.Visibility = Visibility.Hidden
    //   - Hide(this UIElement, bool hide) → Visibility.Hidden / Visible 切换
    //   - Disable(this UIElement) → element.IsEnabled = false
    //   - Enable(this UIElement) → element.IsEnabled = true
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. UIElement → Avalonia.Visual（spike 规范）
    //   2. Visibility 枚举 → Avalonia IsVisible 布尔属性（true=Visible, false=Collapsed）
    //   3. Visibility.Hidden（保留布局空间但不可见）在 Avalonia 11 无等价，
    //      spike 版 Hide() 映射为 IsVisible=false（等同 Collapse，不保留布局空间）
    //   4. IsEnabled 在 Avalonia 中是 InputElement 属性（非 Visual），
    //      spike 版 Disable/Enable 内部 cast 为 InputElement 再设置（Visual 子类如 Control 都实现 InputElement）
    public static class UIElementExtensions
    {
        public static void Show(this Visual element)
        {
            element.IsVisible = true;
        }

        public static void Collapse(this Visual element)
        {
            element.IsVisible = false;
        }

        public static void Hide(this Visual element)
        {
            // spike: Avalonia 11 无 Visibility.Hidden，映射为 IsVisible=false（等同 Collapse）
            element.IsVisible = false;
        }

        public static void Hide(this Visual element, bool hide)
        {
            element.IsVisible = !hide;
        }

        public static void Disable(this Visual element)
        {
            // spike: IsEnabled 在 InputElement 上，Visual 需 cast
            if (element is InputElement input)
            {
                input.IsEnabled = false;
            }
        }

        public static void Enable(this Visual element)
        {
            // spike: IsEnabled 在 InputElement 上，Visual 需 cast
            if (element is InputElement input)
            {
                input.IsEnabled = true;
            }
        }
    }
}
