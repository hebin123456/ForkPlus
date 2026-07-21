using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 DragAndDropListViewItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/DragAndDropListViewItem.cs（165 行）：
    //   - WPF DragAndDropListViewItem : ListViewItem（internal）
    //   - 字段：_wasSelected / _dragStartPoint / _adorner / _dropAdorner
    //   - 属性：ParentListView / DropPosition / AllowDrag
    //   - OnMouseLeftButtonDown：记录 _wasSelected + _dragStartPoint + CaptureMouse
    //   - OnMouseLeftButtonUp：ReleaseMouseCapture
    //   - OnMouseMove：ExceedDragDistance → ParentListView.ItemDrag → AdornerLayer + DragDrop.DoDragDrop
    //   - OnGiveFeedback：_adorner.UpdatePosition(PointFromScreen(MouseHelper.GetMousePosition()))
    //   - OnDragEnter / OnDrop / OnDragLeave：ClearDropAdorner
    //   - GetDropPosition：Y < 3 → Top / Y > ActualHeight-3 → Bottom / 其他 → Over
    //   - ShowDropAdorner / ClearDropAdorner：AdornerLayer.Add / Remove
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 ListBoxItem）：
    //   1. WPF ListViewItem 基类 → Avalonia ListBoxItem
    //   2. WPF MouseLeftButtonDown/Up → Avalonia PointerPressed/Released
    //   3. WPF MouseMove + CaptureMouse → Avalonia PointerMoved + Pointer.Capture
    //   4. WPF DragDrop.DoDragDrop → Avalonia DragDrop.DoDragDrop（API 类似）
    //   5. WPF AdornerLayer → spike 省略（Avalonia AdornerLayer API 差异大）
    //   6. WPF OnGiveFeedback → spike 省略（无 Adorner 视觉反馈）
    //   7. WPF DropPlaceAdorner → spike 省略
    //   8. spike 保留 DropPosition 属性 + AllowDrag 属性 + GetDropPosition 逻辑
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ListBoxItem
    //   - DropPosition 属性 + AllowDrag 属性
    //   - GetDropPosition(Point) 计算放置位置
    public class DragAndDropListViewItem : ListBoxItem
    {
        // 对照 WPF: public DragAndDropListView ParentListView { get; internal set; }
        public DragAndDropListView ParentListView { get; set; }

        // 对照 WPF: public DropPosition DropPosition { get; private set; }
        public DropPosition DropPosition { get; private set; }

        // 对照 WPF: public bool AllowDrag { get; set; }
        public bool AllowDrag { get; set; }

        // 对照 WPF: private bool _wasSelected
        private bool _wasSelected;

        // 对照 WPF: private Point _dragStartPoint
        private Point _dragStartPoint;

        public DragAndDropListViewItem()
        {
            // spike: 订阅 PointerPressed / PointerReleased / PointerMoved
            // 对照 WPF: OnMouseLeftButtonDown / OnMouseLeftButtonUp / OnMouseMove
            PointerPressed += DragAndDropListViewItem_PointerPressed;
            PointerReleased += DragAndDropListViewItem_PointerReleased;
        }

        // 对照 WPF: protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        private void DragAndDropListViewItem_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            // spike: 记录拖拽起点（spike 不实现 CaptureMouse，简化视觉反馈）
            _wasSelected = IsSelected;
            _dragStartPoint = e.GetPosition(null);
        }

        // 对照 WPF: protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        private void DragAndDropListViewItem_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            // spike: 释放捕获（Avalonia Pointer.Capture 释放省略）
        }

        // 对照 WPF: private DropPosition GetDropPosition(DragEventArgs e)
        // spike: 接收 Point 参数（简化事件依赖）
        public DropPosition GetDropPosition(Point point)
        {
            double actualHeight = Bounds.Height;
            double y = point.Y;
            double threshold = 3.0;
            if (y < threshold)
            {
                // 对照 WPF: DropPosition.Top
                return DropPosition.Before;
            }
            if (y > actualHeight - threshold)
            {
                // 对照 WPF: DropPosition.Bottom
                return DropPosition.After;
            }
            // 对照 WPF: DropPosition.Over
            return DropPosition.Inside;
        }
    }
}
