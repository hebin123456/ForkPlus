using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 TreeViewControlItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/TreeViewControlItem.cs（148 行）：
    //   - WPF TreeViewControlItem : ListViewItem
    //   - MultiselectionTreeViewItem Node => base.DataContext as MultiselectionTreeViewItem
    //   - MultiselectionTreeView ParentTreeView { get; internal set; }
    //   - OnPropertyChanged：DataContext 变化 → UpdateDataContext(oldNode, newNode)
    //   - Node_PropertyChanged：IsExpanded && Node.IsExpanded → ParentTreeView.HandleExpanding(Node)
    //   - CalculateIndent() => Math.Max(0, 19 * Node.Level - 19)
    //   - OnMouseLeftButtonDown / OnMouseMove / OnMouseLeftButtonUp（拖放 + CaptureMouse）
    //   - OnGiveFeedback（DragAdorner 位置更新）
    //   - OnDragEnter / OnDragOver / OnDrop / OnDragLeave（转发到 ParentTreeView.HandleDrag*）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 System.Windows.Controls.ListViewItem → Avalonia.Controls.ListBoxItem
    //      （task spec 未指定基类；WPF ListViewItem ≈ Avalonia ListBoxItem，
    //       spike 选 ListBoxItem 以保持 ItemsControl 容器语义）
    //   2. WPF DataContext + OnPropertyChanged(DependencyPropertyChangedEventArgs) →
    //      Avalonia DataContext + OnPropertyChanged(AvaloniaPropertyChangedEventArgs)
    //   3. WPF AdornerLayer + DragAdorner → spike 跳过（spike 不实现拖放）
    //   4. WPF MouseLeftButtonDown/Move/Up → spike 跳过（spike 不实现拖放）
    //   5. WPF DragEnter/Over/Drop/Leave → spike 跳过（spike 不实现拖放）
    //   6. WPF Node_PropertyChanged → ParentTreeView.HandleExpanding → spike 跳过
    //      （spike ParentTreeView 不实现 HandleExpanding）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ListBoxItem
    //   - Node 属性（DataContext as MultiselectionTreeViewItem）
    //   - ParentTreeView 属性
    //   - CalculateIndent() 方法
    public class TreeViewControlItem : ListBoxItem
    {
        // 对照 WPF: public MultiselectionTreeViewItem Node => base.DataContext as MultiselectionTreeViewItem
        public MultiselectionTreeViewItem Node => DataContext as MultiselectionTreeViewItem;

        // 对照 WPF: public MultiselectionTreeView ParentTreeView { get; internal set; }
        public MultiselectionTreeView ParentTreeView { get; internal set; }

        // 对照 WPF: internal double CalculateIndent()
        //   int num = 19 * Node.Level; num -= 19;
        //   if (num < 0) return 0.0; return num;
        // spike 版：保留缩进计算（无 UI 依赖）
        internal double CalculateIndent()
        {
            if (Node == null)
            {
                return 0.0;
            }
            int num = 19 * Node.Level;
            num -= 19;
            if (num < 0)
            {
                return 0.0;
            }
            return num;
        }
    }
}
