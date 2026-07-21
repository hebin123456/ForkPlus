using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.0b：Avalonia 版 ForkPlusDialogFooter（从 WPF 工程迁移）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/ForkPlusDialogFooter.xaml.cs（43 行）：
    //   - 2 个 event：Cancel / Submit
    //   - AlignStatusRight()：把 StatusMessage/BusyIndicator/StatusImage 从 Left 改到 Right
    //   - CancelButton_Click / SubmitButton_Click：触发对应 event
    //
    // Avalonia 版差异：
    //   1. spike 版不实现 AlignStatusRight（spike 用 Grid 固定布局，无需切换 dock；
    //      若子类需要可 Phase 4.0c 补 HorizontalAlignment 切换）
    //   2. event 签名与 WPF 一致（EventHandler）
    //
    // 本 spike 版验证：
    //   - Submit/Cancel 按钮点击可触发事件
    //   - 外部 ForkPlusDialogWindow 可订阅 Submit/Cancel 事件
    public partial class ForkPlusDialogFooter : UserControl
    {
        public event EventHandler? Cancel;
        public event EventHandler? Submit;

        public ForkPlusDialogFooter()
        {
            InitializeComponent();
        }

        // 对照 WPF: public void AlignStatusRight() — spike 版未迁移
        // spike 版用 Grid 固定布局，不需要切换 dock
        // Phase 4.0c 若子类需要可补 HorizontalAlignment 切换

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Cancel?.Invoke(this, EventArgs.Empty);
        }

        private void SubmitButton_Click(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Submit?.Invoke(this, EventArgs.Empty);
        }
    }
}
