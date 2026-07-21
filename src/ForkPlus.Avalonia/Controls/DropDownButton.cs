using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 DropDownButton（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/DropDownButton.cs（29 行）：
    //   - WPF DropDownButton : ToggleButton
    //   - OnChecked：ContextMenu.PlacementTarget = this + Placement = Bottom +
    //     订阅 Closed + IsOpen = true + IsChecked = true
    //   - OnUnchecked：取消订阅 Closed + IsOpen = false
    //   - ContextMenu_Closed：IsChecked = false（关闭菜单时取消勾选）
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 Button + Flyout）：
    //   1. WPF ToggleButton + ContextMenu → Avalonia Button + Flyout
    //      （task spec 明确要求：继承 Button + Flyout）
    //   2. WPF OnChecked/OnUnchecked 虚方法 → Avalonia Click 事件 + Flyout.Show/Hide
    //   3. WPF ContextMenu.Closed → ContextMenu_Closed → IsChecked = false
    //      Avalonia Flyout.Closed 事件 → 同样逻辑（spike 版无需 IsChecked，因基类为 Button）
    //   4. WPF ContextMenu.Placement = PlacementMode.Bottom → Avalonia Flyout.Placement
    //      （FlyoutBase.Placement = PlacementMode.Bottom）
    //   5. spike 用 ContextMenu 属性兼容（Avalonia Button.ContextMenu 不存在，
    //      spike 改用 SetContextMenu(IReadOnlyList<Control>) 公共方法注入菜单项）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 Button + Flyout（MenuFlyout）
    //   - SetContextMenu(MenuItem[]) 公共方法注入菜单项
    //   - Click 事件显示 Flyout
    //   - Flyout.Closed 事件（无需同步 IsChecked，基类为 Button）
    public class DropDownButton : Button
    {
        // 对照 WPF: private ContextMenu ContextMenu (base.ContextMenu)
        // spike 版：用 MenuFlyout 替代 ContextMenu
        private MenuFlyout _flyout;

        public DropDownButton()
        {
            // 对照 WPF: OnChecked → ContextMenu.IsOpen = true
            // spike 版：Click 事件显示 Flyout
            Click += DropDownButton_Click;
        }

        // spike 新增：注入菜单项（替代 WPF base.ContextMenu）
        // 对照 WPF: base.ContextMenu.Items.Add(menuItem) 隐式
        public void SetContextMenu(global::Avalonia.Controls.MenuItem[] items)
        {
            if (_flyout == null)
            {
                _flyout = new MenuFlyout
                {
                    Placement = global::Avalonia.Controls.PlacementMode.Bottom
                };
                // 对照 WPF: base.ContextMenu.Closed += ContextMenu_Closed
                _flyout.Closed += (s, e) =>
                {
                    // 对照 WPF: ContextMenu_Closed → IsChecked = false
                    // spike 版：基类为 Button，无 IsChecked，仅触发 Closed 事件
                    // Avalonia 11 无 RoutedEventHandler 类型，改用 EventHandler<RoutedEventArgs>
                    // Flyout.Closed 事件参数为 EventArgs，spike 新建 RoutedEventArgs 传递
                    FlyoutClosed?.Invoke(this, new RoutedEventArgs());
                };
            }
            _flyout.Items.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    _flyout.Items.Add(item);
                }
            }
        }

        // 对照 WPF: OnChecked → ContextMenu.IsOpen = true
        private void DropDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_flyout != null)
            {
                _flyout.ShowAt(this);
            }
        }

        // spike 新增：Flyout 关闭事件（对照 WPF ContextMenu_Closed）
        // Avalonia 11 无 RoutedEventHandler 类型，改用 EventHandler<EventArgs>
        // （FlyoutBase.Closed 事件参数为 EventArgs）
        public event EventHandler<EventArgs> FlyoutClosed;
    }
}
