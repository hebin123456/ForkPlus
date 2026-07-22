using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/ItemsControlExtensions.cs（43 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - GetObjectAtPoint<ItemContainer>(this ItemsControl, Point) → GetContainerAtPoint + ItemContainerGenerator.ItemFromContainer
    //   - GetContainerAtPoint<ItemContainer>(this ItemsControl, Point) → VisualTreeHelper.HitTest + 向上遍历找 ItemContainer
    //   - FocusSelectedItem(this Selector) → ItemContainerGenerator.ContainerFromIndex + Keyboard.Focus
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. ItemsControl → Avalonia.Controls.ItemsControl
    //   2. DependencyObject 约束 → AvaloniaObject（spike 规范）
    //   3. VisualTreeHelper.HitTest → InputElement.InputHitTest(Point)
    //      （Avalonia 11 用实例方法 InputHitTest 替代 WPF 静态 VisualTreeHelper.HitTest）
    //   4. VisualTreeHelper.GetParent → IVisual.GetVisualParent()（Avalonia.VisualTree 扩展方法）
    //   5. Selector → SelectingItemsControl（Avalonia 11 中 Selector 的等价基类）
    //   6. Keyboard.Focus(element) → element.Focus()（Avalonia 11 用实例方法 Focus 替代 Keyboard 静态类）
    //   7. ItemContainerGenerator.ItemFromContainer 接收 Control（Avalonia 11），需 cast ItemContainer → Control
    public static class ItemsControlExtensions
    {
        public static object GetObjectAtPoint<ItemContainer>(this ItemsControl control, Point p) where ItemContainer : AvaloniaObject
        {
            var container = control.GetContainerAtPoint<ItemContainer>(p);
            if (container is Control c)
            {
                int index = control.ItemContainerGenerator.IndexFromContainer(c);
                return index >= 0 ? control.Items[index] : null;
            }
            return null;
        }

        public static ItemContainer GetContainerAtPoint<ItemContainer>(this ItemsControl control, Point p) where ItemContainer : AvaloniaObject
        {
            // spike: VisualTreeHelper.HitTest → InputHitTest + GetVisualParent 遍历
            var hit = control.InputHitTest(p) as Visual;
            while (hit != null && !(hit is ItemContainer))
            {
                hit = hit.GetVisualParent();
            }
            return hit is ItemContainer result ? result : null;
        }

        public static void FocusSelectedItem(this SelectingItemsControl control)
        {
            // spike: Selector → SelectingItemsControl，Keyboard.Focus → element.Focus()
            if (control.SelectedIndex >= 0 && control.ItemContainerGenerator.ContainerFromIndex(control.SelectedIndex) is IInputElement element)
            {
                element.Focus();
            }
        }
    }
}
