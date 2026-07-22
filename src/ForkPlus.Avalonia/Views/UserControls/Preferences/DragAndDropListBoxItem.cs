// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/DragAndDropListBoxItem.cs（147 行）：
//   - public class DragAndDropListBoxItem : ListBoxItem
//   - 字段：bool _wasSelected / Point _dragStartPoint /
//     DragAndDropListBoxAdorner _adorner / DropPlaceAdorner _dropAdorner
//   - 属性：DragAndDropListBox ParentListBox / DropPosition DropPosition
//   - OnMouseLeftButtonDown：记录 _wasSelected + _dragStartPoint + CaptureMouse
//   - OnMouseLeftButtonUp：ReleaseMouseCapture + 恢复选中状态
//   - OnMouseDoubleClick：e.Handled=true
//   - OnMouseMove：ExceedDragDistance 检查 + CompactMap SelectedItems +
//     ItemContainerGenerator.ContainerFromItem + DragAndDropListBoxAdorner +
//     AdornerLayer.GetAdornerLayer + DragDrop.DoDragDrop
//   - OnGiveFeedback：MouseHelper.GetMousePosition + _adorner.UpdatePosition
//   - ExceedDragDistance：SystemParameters.MinimumHorizontalDragDistance / MinimumVerticalDragDistance
//   - OnDragEnter / OnDrop / OnDragLeave：DropPlaceAdorner 显示/清除
//   - GetDropPositoion：上半 → Top，下半 → Bottom
//   - ShowDropAdorner / ClearDropAdorner：AdornerLayer 添加/移除 DropPlaceAdorner
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ListBoxItem → Avalonia.Controls.ListBoxItem（同名，API 略不同）
//   2. WPF AdornerLayer / DragAndDropListBoxAdorner / DropPlaceAdorner → spike 移除
//      （Avalonia 无 AdornerLayer 等价物，拖拽视觉反馈留 Phase 3.9b 用 Canvas 叠加层实现）
//   3. WPF DragDrop.DoDragDrop → spike 移除（Avalonia 用 DragDrop.DoDragDrop，
//      但签名不同且依赖 DataObject，spike 阶段不接入拖拽功能）
//   4. WPF Mouse.LeftButton / CaptureMouse / ReleaseMouseCapture →
//      Avalonia PointerPressed/Released + Pointer.Capture（spike 阶段不捕获）
//   5. WPF OnMouseLeftButtonDown/Up → Avalonia OnPointerPressed/Released
//   6. WPF OnMouseMove → Avalonia OnPointerMoved
//   7. WPF OnMouseDoubleClick → spike 移除（Avalonia 用 DoubleTappedEvent，
//      ListBoxItem 无 OnPointerDoubleTapped 虚方法，spike 用 AddHandler(DoubleTappedEvent)）
//   8. WPF OnDragEnter/Drop/Leave → spike 移除（Avalonia ListBoxItem 无 OnDragOver/Drop/Leave
//      虚方法，用 AddHandler(DragDrop.DragOverEvent/DropEvent) 替代，spike 阶段不接入）
//   9. WPF OnGiveFeedback → spike 移除（Avalonia 无 GiveFeedback 事件）
//  10. WPF SystemParameters.MinimumHorizontalDragDistance → Avalonia 无等价，
//      spike 用硬编码 4.0（SystemDragMinimum）
//  11. WPF ItemContainerGenerator.ContainerFromItem → Avalonia ContainerFromIndex
//      或 ItemContainerGenerator（spike 阶段不使用）
//  12. DropPosition 枚举：从 ForkPlus.UI.Controls 迁入本命名空间（WPF 版在 Controls/ 下）
//  13. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
//
// spike 简化（task spec：复杂渲染逻辑可简化为空实现 + 注释）：
//   - ParentListBox / DropPosition 属性保留
//   - OnPointerPressed/Released/Moved：保留方法签名 + 简化实现
//     （拖拽启动逻辑留 Phase 3.9b）
//   - ExceedDragDistance：保留完整逻辑（纯 C#，用硬编码阈值）
//   - GetDropPositoion：保留完整逻辑（纯 C#，用 Bounds.Height 替代 ActualHeight）
using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    // 对照 WPF: ForkPlus.UI.Controls.DropPosition（Top/Bottom/Over）
    // spike 版：迁入本命名空间（WPF 版在 Controls/ 下，Avalonia 工程不迁 Controls/）
    public enum DropPosition
    {
        Top,
        Bottom,
        Over
    }

    public class DragAndDropListBoxItem : ListBoxItem
    {
        // 对照 WPF: private bool _wasSelected;
        private bool _wasSelected;

        // 对照 WPF: private Point _dragStartPoint;
        private Point _dragStartPoint;

        // 对照 WPF: public DragAndDropListBox ParentListBox { get; internal set; }
        public DragAndDropListBox ParentListBox { get; internal set; }

        // 对照 WPF: public DropPosition DropPosition { get; internal set; }
        public DropPosition DropPosition { get; internal set; }

        // 对照 WPF: protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        // Avalonia: protected override void OnPointerPressed(PointerPressedEventArgs e)
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            _wasSelected = IsSelected;
            // 对照 WPF: if (Mouse.LeftButton == MouseButtonState.Pressed)
            //           { _dragStartPoint = e.GetPosition(null); CaptureMouse(); }
            // spike 版：记录拖拽起点，spike 阶段不捕获鼠标（CaptureMouse → e.Pointer.Capture）
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _dragStartPoint = e.GetPosition(null);
                // Phase 3.9b 在此补：e.Pointer.Capture(this);
            }
        }

        // 对照 WPF: protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        // Avalonia: protected override void OnPointerReleased(PointerReleasedEventArgs e)
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            // 对照 WPF: ReleaseMouseCapture();
            // spike 版：spike 阶段未捕获鼠标，无需释放
            // Phase 3.9b 在此补：e.Pointer.Capture(null);
            if (_wasSelected)
            {
                IsSelected = false;
            }
        }

        // 对照 WPF: protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        // spike 版：Avalonia ListBoxItem 无 OnPointerDoubleTapped 虚方法，
        // 用 AddHandler(DoubleTappedEvent) 在构造函数中订阅（spike 阶段不订阅）
        // Phase 3.9b 在此补：AddHandler(DoubleTappedEvent, (s, e) => { e.Handled = true; });

        // 对照 WPF: protected override void OnMouseMove(MouseEventArgs e)
        // Avalonia: protected override void OnPointerMoved(PointerEventArgs e)
        // spike 版：保留方法签名 + 简化实现（拖拽启动 + Adorner + DragDrop 留 Phase 3.9b）
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            // Phase 3.9b 在此补：
            //   - 检查 IsPointerOver + e.Pointer.Captured == this
            //   - ExceedDragDistance 检查
            //   - ParentListBox.SelectedItems 取选中项
            //   - ContainerFromItem 取 ListBoxItem
            //   - 创建 DragAdorner（Canvas 叠加层）+ DragDrop.DoDragDrop
        }

        // 对照 WPF: private static bool ExceedDragDistance(Vector diff)
        // spike 版：用硬编码 4.0 替代 SystemParameters.MinimumHorizontalDragDistance
        private static bool ExceedDragDistance(Vector diff)
        {
            const double minimumDragDistance = 4.0;
            if (!(Math.Abs(diff.X) > minimumDragDistance))
            {
                return Math.Abs(diff.Y) > minimumDragDistance;
            }
            return true;
        }

        // 对照 WPF: OnDragEnter / OnDrop / OnDragLeave → DropPlaceAdorner 显示/清除
        // spike 版：Avalonia ListBoxItem 无 OnDragOver/Drop/Leave 虚方法，
        // 用 AddHandler(DragDrop.DragOverEvent/DropEvent) 替代，spike 阶段不接入
        // Phase 3.9b 在此补：
        //   - 构造函数中 AddHandler(DragDrop.DragOverEvent, OnDragOver)
        //   - 构造函数中 AddHandler(DragDrop.DropEvent, OnDrop)
        //   - 构造函数中 AddHandler(DragDrop.DragLeaveEvent, OnDragLeave)
        //   - OnDragOver: ClearDropAdorner + GetDropPositoion + ShowDropAdorner
        //   - OnDrop: ClearDropAdorner
        //   - OnDragLeave: ClearDropAdorner

        // 对照 WPF: private DropPosition GetDropPositoion(DragEventArgs e)
        // spike 版：保留完整逻辑（纯 C#，用 Bounds.Height 替代 ActualHeight）
        private DropPosition GetDropPositoion(DragEventArgs e)
        {
            double y = e.GetPosition(this).Y;
            double actualHeight = Bounds.Height;
            if (!(y < actualHeight / 2.0))
            {
                return DropPosition.Bottom;
            }
            return DropPosition.Top;
        }
    }
}
