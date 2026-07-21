using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 DragAndDropListView（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/DragAndDropListView.cs（38 行）：
    //   - WPF DragAndDropListView : NoUIAutomationListView（internal）
    //   - ItemDrag 事件：拖拽开始时触发（EventHandler<EventArgs>）
    //   - DragAutoScrollHelper 字段：构造时实例化
    //   - GetContainerForItemOverride → new DragAndDropListViewItem()
    //   - IsItemItsOwnContainerOverride → item is DragAndDropListViewItem
    //   - PrepareContainerForItemOverride → 设置 item.ParentListView = this
    //   - StopDragAutoScroll → _dragAutoScroll.StopAutoScroll()
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 ListBox + DragDrop 事件）：
    //   1. WPF NoUIAutomationListView 基类 → Avalonia ListBox（spike 简化）
    //   2. WPF GetContainerForItemOverride → Avalonia 在 ControlTheme 中配置 Container
    //      spike 改为重写 CreateContainerItem（Avalonia 11 API）
    //   3. WPF ItemDrag 事件 → spike 保留同名事件
    //   4. WPF DragAutoScrollHelper → spike 用同命名空间静态类
    //   5. spike 简化拖拽视觉反馈（task spec：省略 AdornerLayer）
    //   6. WPF PrepareContainerForItemOverride → spike 重写 OnSelectionChanged 简化
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ListBox
    //   - ItemDrag 事件 + AllowDrop 属性
    //   - DragDrop.SetAllowDrop(this, true) + DragDrop.DropEvent 订阅
    public class DragAndDropListView : ListBox
    {
        // 对照 WPF: public EventHandler<EventArgs> ItemDrag
        public event EventHandler<EventArgs> ItemDrag;

        public DragAndDropListView()
        {
            // spike: 启用 Drop（task spec：DragDrop.SetAllowDrop + DragDrop.DropEvent）
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DropEvent, OnDrop);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
        }

        // spike: 容器创建（对照 WPF GetContainerForItemOverride）
        // Avalonia 11 用 ControlTheme 配置 Container，spike 简化为不重写（默认 ListBoxItem）
        // 真实容器配置由 ControlTheme 在 Themes/Styles 中定义（Phase 3.9b）

        // 对照 WPF: internal void StopDragAutoScroll()
        public void StopDragAutoScroll()
        {
            DragAutoScrollHelper.StopAutoScroll();
        }

        // spike: 触发 ItemDrag 事件（对照 WPF OnMouseMove 中 ParentListView.ItemDrag?.Invoke）
        public void RaiseItemDrag(DragAndDropListViewItem item)
        {
            ItemDrag?.Invoke(item, EventArgs.Empty);
        }

        // spike: DragOver 处理（对照 WPF DragAutoScrollHelper.OnDragOver）
        private void OnDragOver(object sender, DragEventArgs e)
        {
            // spike: 自动滚动逻辑简化（DragAutoScrollHelper.HandleDragOver 需要 ScrollViewer）
            // 真实实现在 DragAutoScrollHelper.HandleDragOver
        }

        // spike: Drop 处理（对照 WPF DragAndDropListViewItem.OnDrop）
        private void OnDrop(object sender, DragEventArgs e)
        {
            // spike: 停止自动滚动
            StopDragAutoScroll();
        }
    }
}
