using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/VisualTreeAttachmentHelper.cs（175 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI，internal static class）：
    //   - TryAddChild(Panel, UIElement, string) → PrepareForNewParent + panel.Children.Add
    //   - TrySetChild(Decorator, UIElement, string) → PrepareForNewParent + decorator.Child = child
    //   - TrySetPopupChild(Popup, UIElement, string) → PrepareForNewParent + popup.Child = child
    //   - TrySetContent(ContentControl, object, string) → PrepareForNewParent + contentControl.Content = content
    //   - PrepareForNewParent(DependencyObject, string) → 检查旧父级 + DetachFromParent
    //   - Describe(DependencyObject) → 类型名 + Name（FrameworkElement.Name / FrameworkContentElement.Name）
    //   - private GetParent(DependencyObject) → LogicalTreeHelper.GetParent + VisualTreeHelper.GetParent
    //   - private DetachFromParent(DependencyObject, DependencyObject) → 按 Popup/Panel/Decorator/
    //     HeaderedContentControl/HeaderedItemsControl/ContentControl/ContentPresenter/ItemsControl 分离
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. DependencyObject → AvaloniaObject（spike 规范）
    //   2. UIElement → Visual（spike 规范），但 Panel.Children / Popup.Child 需 Control 类型，内部 cast
    //   3. Decorator 在 Avalonia 11 无等价类，TrySetChild 暂注释
    //   4. HeaderedContentControl / HeaderedItemsControl 在 Avalonia 11 无等价基类，DetachFromParent 跳过
    //   5. LogicalTreeHelper.GetParent → ILogical.LogicalParent（Avalonia.LogicalTree 扩展）
    //   6. VisualTreeHelper.GetParent → IVisual.GetVisualParent()（Avalonia.VisualTree 扩展）
    //   7. FrameworkElement.Name → StyledElement.Name（Avalonia 11 等价）
    //   8. FrameworkContentElement.Name 无 Avalonia 等价，Describe 跳过
    //   9. Log.Warn 来自 ForkPlus.Core（ForkPlus 命名空间，父命名空间查找可用）
    internal static class VisualTreeAttachmentHelper
    {
        public static bool TryAddChild(Panel panel, Visual child, string targetDescription)
        {
            if (panel == null)
            {
                return false;
            }
            if (child == null)
            {
                return true;
            }
            if (!PrepareForNewParent(child, targetDescription))
            {
                return false;
            }
            // spike: Panel.Children 需 Control 类型，Visual 需 cast
            if (child is Control c)
            {
                panel.Children.Add(c);
                return true;
            }
            return false;
        }

        // spike: WPF Decorator 在 Avalonia 11 无等价类，TrySetChild 暂注释
        // public static bool TrySetChild(Decorator decorator, Visual child, string targetDescription)

        public static bool TrySetPopupChild(Popup popup, Visual child, string targetDescription)
        {
            if (popup == null)
            {
                return false;
            }
            if (child != null && !PrepareForNewParent(child, targetDescription))
            {
                return false;
            }
            // spike: Popup.Child 需 Control 类型，Visual 需 cast
            if (child == null)
            {
                popup.Child = null;
                return true;
            }
            if (child is Control c)
            {
                popup.Child = c;
                return true;
            }
            return false;
        }

        public static bool TrySetContent(ContentControl contentControl, object content, string targetDescription)
        {
            if (contentControl == null)
            {
                return false;
            }
            if (content is AvaloniaObject dependencyObject && !PrepareForNewParent(dependencyObject, targetDescription))
            {
                return false;
            }
            contentControl.Content = content;
            return true;
        }

        public static bool PrepareForNewParent(AvaloniaObject child, string targetDescription)
        {
            if (child == null)
            {
                return true;
            }
            AvaloniaObject parent = GetParent(child);
            if (parent == null)
            {
                return true;
            }
            if (!DetachFromParent(child, parent))
            {
                if (GetParent(child) == null)
                {
                    return true;
                }
                Log.Warn("Cannot detach " + Describe(child) + " from " + Describe(parent) + " before attaching to " + targetDescription + ".");
                return false;
            }
            AvaloniaObject parent2 = GetParent(child);
            if (parent2 != null)
            {
                Log.Warn("Detached " + Describe(child) + " from " + Describe(parent) + " but it is still parented by " + Describe(parent2) + " before attaching to " + targetDescription + ".");
                return false;
            }
            return true;
        }

        public static string Describe(AvaloniaObject item)
        {
            if (item == null)
            {
                return "null";
            }
            // spike: WPF FrameworkElement.Name → Avalonia StyledElement.Name
            if (item is StyledElement styledElement && !string.IsNullOrEmpty(styledElement.Name))
            {
                return item.GetType().Name + "('" + styledElement.Name + "')";
            }
            // spike: WPF FrameworkContentElement.Name 无 Avalonia 等价，跳过
            return item.GetType().Name;
        }

        private static AvaloniaObject GetParent(AvaloniaObject child)
        {
            // spike: WPF LogicalTreeHelper.GetParent → ILogical.LogicalParent
            if (child is ILogical logical && logical.LogicalParent is AvaloniaObject logicalAo)
            {
                return logicalAo;
            }
            // spike: WPF VisualTreeHelper.GetParent → IVisual.GetVisualParent()
            if (child is Visual visual && visual.GetVisualParent() is AvaloniaObject visualAo)
            {
                return visualAo;
            }
            return null;
        }

        private static bool DetachFromParent(AvaloniaObject child, AvaloniaObject parent)
        {
            // spike: Popup.Child
            if (parent is Popup popup && child is Control c1 && ReferenceEquals(popup.Child, c1))
            {
                popup.Child = null;
                return true;
            }
            // spike: Panel.Children.Remove
            if (parent is Panel panel && child is Control c2 && panel.Children.Contains(c2))
            {
                panel.Children.Remove(c2);
                return true;
            }
            // spike: WPF Decorator → Avalonia 无等价，跳过
            // spike: WPF HeaderedContentControl.Header → Avalonia 无等价基类，跳过
            // spike: WPF HeaderedItemsControl.Header → Avalonia 无等价基类，跳过
            // spike: ContentControl.Content
            if (parent is ContentControl contentControl && ReferenceEquals(contentControl.Content, child))
            {
                contentControl.Content = null;
                return true;
            }
            // spike: ContentPresenter.Content
            if (parent is ContentPresenter contentPresenter && ReferenceEquals(contentPresenter.Content, child))
            {
                contentPresenter.Content = null;
                return true;
            }
            // spike: ItemsControl.Items.Remove
            if (parent is ItemsControl itemsControl && itemsControl.Items.IndexOf(child) >= 0)
            {
                itemsControl.Items.Remove(child);
                return true;
            }
            return false;
        }
    }
}
