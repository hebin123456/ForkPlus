using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 AdvancedTooltipButton（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/AdvancedTooltipButton.cs（112 行）：
    //   - WPF AdvancedTooltipButton : Button
    //   - 构造函数接收 RepositoryUserControl + Sha + Action
    //   - DispatcherTimer _showPopupTimer（500ms）/ _closePopupTimer（100ms）
    //   - Click → ClosePopup(hardClose) + _action()
    //   - MouseEnter → 启动 _showPopupTimer / MouseLeave → 启动 _closePopupTimer
    //   - ShowPopup：创建 Popup + TooltipRevisionDetailsUserControl
    //     + VisualTreeAttachmentHelper.TrySetPopupChild
    //   - ClosePopup：_popup.IsOpen = false + TrySetPopupChild(null)
    //   - CreatePopup：Popup + AllowsTransparency + PopupAnimation.Fade + PlacementTarget
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 Button + ToolTip.SetTip）：
    //   1. WPF DispatcherTimer → Avalonia DispatcherTimer（Avalonia.Threading 命名空间）
    //      注意：因 RootNamespace=ForkPlus.Avalonia，需用 global::Avalonia.Threading 限定
    //   2. WPF Popup + TooltipRevisionDetailsUserControl（自定义 UserControl）→
    //      spike 用 ToolTip.SetTip 简化（task spec 明确要求）
    //   3. WPF MouseEnter/MouseLeave (MouseEventArgs) → Avalonia PointerEnter/PointerLeave
    //   4. WPF VisualTreeAttachmentHelper.TrySetPopupChild → spike 跳过（用 ToolTip 替代）
    //   5. spike 跳过 RepositoryUserControl / Sha 类型依赖（WPF 工程 ForkPlus.UI 不可访问）
    //      改用 SetTooltipContent(object) 公共方法注入 tooltip 内容
    //   6. WPF Popup.AllowsTransparency + PopupAnimation.Fade → spike 跳过（ToolTip 内置动画）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 Button + ToolTip.SetTip
    //   - 构造函数接收 Action（task spec 简化：去掉 RepositoryUserControl / Sha）
    //   - SetTooltipContent(object) 公共方法注入 tooltip 内容
    //   - Click 事件转发到注入的 Action
    public class AdvancedTooltipButton : Button
    {
        // 对照 WPF: private readonly DispatcherTimer _showPopupTimer (500ms)
        private readonly global::Avalonia.Threading.DispatcherTimer _showPopupTimer;

        // 对照 WPF: private readonly DispatcherTimer _closePopupTimer (100ms)
        private readonly global::Avalonia.Threading.DispatcherTimer _closePopupTimer;

        // 对照 WPF: private readonly Action _action
        private readonly Action _action;

        // 对照 WPF: private Popup _popup
        // spike 版：用 object 字段保存 tooltip 内容（替代 Popup）
        private object _tooltipContent;

        // 对照 WPF: public AdvancedTooltipButton(RepositoryUserControl, Sha, Action)
        // spike 版简化签名：仅接收 Action（task spec 简化策略）
        public AdvancedTooltipButton(Action action)
        {
            _action = action;

            // 对照 WPF: _showPopupTimer.Interval = TimeSpan.FromMilliseconds(500.0)
            _showPopupTimer = new global::Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500.0)
            };
            _showPopupTimer.Tick += ShowPopupTimer_Tick;

            // 对照 WPF: _closePopupTimer.Interval = TimeSpan.FromMilliseconds(100.0)
            _closePopupTimer = new global::Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100.0)
            };
            _closePopupTimer.Tick += ClosePopupTimer_Tick;

            // 对照 WPF: base.Click += AdvancedTooltipButton_Click
            Click += AdvancedTooltipButton_Click;

            // 对照 WPF: base.MouseEnter += delegate { _closePopupTimer.Stop(); _showPopupTimer.Start(); }
            // Avalonia 11：PointerEntered 替代 MouseEnter
            PointerEntered += (s, e) =>
            {
                e.Handled = true;
                _closePopupTimer.Stop();
                _showPopupTimer.Start();
            };

            // 对照 WPF: base.MouseLeave += delegate { _showPopupTimer.Stop(); _closePopupTimer.Start(); }
            // Avalonia 11：PointerExited 替代 MouseLeave
            PointerExited += (s, e) =>
            {
                e.Handled = true;
                _showPopupTimer.Stop();
                _closePopupTimer.Start();
            };
        }

        // 对照 WPF: private void AdvancedTooltipButton_Click(object sender, RoutedEventArgs e)
        //   ClosePopup(hardClose: true) + _showPopupTimer.Stop() + _action()
        private void AdvancedTooltipButton_Click(object sender, RoutedEventArgs e)
        {
            ClosePopup();
            _showPopupTimer.Stop();
            _action?.Invoke();
        }

        // 对照 WPF: private void _showPopupTimer_Tick(object sender, EventArgs e)
        //   ShowPopup() + _showPopupTimer.Stop()
        private void ShowPopupTimer_Tick(object sender, EventArgs e)
        {
            ShowPopup();
            _showPopupTimer.Stop();
        }

        // 对照 WPF: private void _closePopupTimer_Tick(object sender, EventArgs e)
        //   ClosePopup() + _closePopupTimer.Stop()
        private void ClosePopupTimer_Tick(object sender, EventArgs e)
        {
            ClosePopup();
            _closePopupTimer.Stop();
        }

        // 对照 WPF: private void ShowPopup()
        //   if (_popup == null || !_popup.IsOpen) { _popup = CreatePopup(); _popup.IsOpen = true; }
        // spike 版：用 ToolTip.SetTip 显示 tooltip 内容
        private void ShowPopup()
        {
            if (_tooltipContent != null)
            {
                ToolTip.SetTip(this, _tooltipContent);
            }
        }

        // 对照 WPF: private void ClosePopup(bool hardClose = false)
        //   if (_popup != null && _popup.IsOpen && (!_popup.IsMouseOver || hardClose)) {
        //     _popup.IsOpen = false; TrySetPopupChild(_popup, null); _popup = null; }
        // spike 版：清空 ToolTip
        private void ClosePopup()
        {
            ToolTip.SetTip(this, null);
        }

        // spike 新增：注入 tooltip 内容（替代 WPF 构造函数中的 TooltipRevisionDetailsUserControl）
        // 对照 WPF: CreatePopup() 内部创建 TooltipRevisionDetailsUserControl
        public void SetTooltipContent(object content)
        {
            _tooltipContent = content;
        }
    }
}
