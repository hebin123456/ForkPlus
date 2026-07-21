using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 CommandHyperlink（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/CommandHyperlink.cs（126 行）：
    //   - WPF CommandHyperlink : Hyperlink（System.Windows.Documents.Hyperlink）
    //   - 构造函数接收 RepositoryUserControl + Sha + string text + Action
    //     base(new Run(text)) 初始化 Hyperlink 文本
    //   - DispatcherTimer _showPopupTimer（500ms）/ _closePopupTimer（100ms）
    //   - Style = Application.Current.TryFindResource("BugtrackerHyperlinkStyle")
    //   - Click → ClosePopup(hardClose) + _action()
    //   - MouseEnter → 启动 _showPopupTimer / MouseLeave → 启动 _closePopupTimer
    //   - ShowPopup / ClosePopup / CreatePopup：与 AdvancedTooltipButton 同模式
    //     + PlacementRectangle（基于 ElementStart/ElementEnd GetCharacterRect）
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 Button + 样式为超链接）：
    //   1. WPF Hyperlink（System.Windows.Documents）→ Avalonia Button（Avalonia 无 Hyperlink 类）
    //      spike 用 Button + 超链接样式（task spec 明确要求）
    //   2. WPF base(new Run(text)) → Avalonia Button.Content = text
    //   3. WPF DispatcherTimer → global::Avalonia.Threading.DispatcherTimer
    //      （因 RootNamespace=ForkPlus.Avalonia，需 global:: 限定）
    //   4. WPF Popup + TooltipRevisionDetailsUserControl → spike 用 ToolTip.SetTip 简化
    //   5. WPF MouseEnter/MouseLeave → Avalonia PointerEnter/PointerLeave
    //   6. WPF ElementStart.GetCharacterRect / ElementEnd.GetCharacterRect → spike 跳过
    //      （Avalonia 无字符级 rect API，spike 用 PlacementTarget=this 兜底）
    //   7. spike 跳过 RepositoryUserControl / Sha 类型依赖
    //   8. spike 跳过 BugtrackerHyperlinkStyle 资源查找（Theme 未迁移）
    //      默认超链接外观：Foreground=Blue + Cursor=Hand + Underline
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 Button + Content = text + 超链接样式（蓝字 + Hand cursor）
    //   - 构造函数接收 string text + Action
    //   - Click 事件转发到注入的 Action
    //   - SetTooltipContent(object) 公共方法注入 tooltip 内容
    public class CommandHyperlink : Button
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

        // 对照 WPF: public CommandHyperlink(RepositoryUserControl, Sha, string text, Action)
        //   : base(new Run(text))
        // spike 版简化签名：仅接收 string text + Action（task spec 简化策略）
        public CommandHyperlink(string text, Action action)
        {
            _action = action;

            // 对照 WPF: base(new Run(text))
            Content = text;

            // 对照 WPF: base.Style = Application.Current.TryFindResource("BugtrackerHyperlinkStyle")
            // spike 版：硬编码超链接外观（蓝字 + Hand cursor）
            Foreground = global::Avalonia.Media.Brushes.Blue;
            Cursor = new global::Avalonia.Input.Cursor(StandardCursorType.Hand);
            Background = null;
            BorderThickness = new global::Avalonia.Thickness(0);
            Padding = new global::Avalonia.Thickness(0);

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

            // 对照 WPF: base.Click += CommandHyperlink_Click
            Click += CommandHyperlink_Click;

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

        // 对照 WPF: private void CommandHyperlink_Click(object sender, RoutedEventArgs e)
        //   ClosePopup(hardClose: true) + _showPopupTimer.Stop() + _action()
        private void CommandHyperlink_Click(object sender, RoutedEventArgs e)
        {
            ClosePopup();
            _showPopupTimer.Stop();
            _action?.Invoke();
        }

        // 对照 WPF: private void _showPopupTimer_Tick(object sender, EventArgs e)
        private void ShowPopupTimer_Tick(object sender, EventArgs e)
        {
            ShowPopup();
            _showPopupTimer.Stop();
        }

        // 对照 WPF: private void _closePopupTimer_Tick(object sender, EventArgs e)
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
        // spike 版：清空 ToolTip
        private void ClosePopup()
        {
            ToolTip.SetTip(this, null);
        }

        // spike 新增：注入 tooltip 内容
        public void SetTooltipContent(object content)
        {
            _tooltipContent = content;
        }
    }
}
