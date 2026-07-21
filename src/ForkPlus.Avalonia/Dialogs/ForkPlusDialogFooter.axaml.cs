using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.0b：Avalonia 版 ForkPlusDialogFooter（从 WPF 工程迁移，升级版）。
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
    //   3. task spec 关键 API 升级：新增 StatusMessage / ShowSubmitButton / ShowCancelButton /
    //      SubmitButtonTitle / CancelButtonTitle / SetStatus 便捷属性与方法，
    //      使 Footer 自包含（ForkPlusDialogWindow 仍保留同名 API 转发）
    //
    // 本 spike 版验证：
    //   - Submit/Cancel 按钮点击可触发事件
    //   - 外部 ForkPlusDialogWindow 可订阅 Submit/Cancel 事件
    //   - SetStatus 可控制 StatusMessage/StatusImage/BusyIndicator
    //   - ShowSubmitButton / ShowCancelButton / SubmitButtonTitle / CancelButtonTitle 便捷属性
    public partial class ForkPlusDialogFooter : UserControl
    {
        public event EventHandler? Cancel;
        public event EventHandler? Submit;

        public ForkPlusDialogFooter()
        {
            InitializeComponent();
        }

        // ===== task spec 关键 API：便捷属性 =====

        // 对照 WPF: StatusMessage（ForkPlusDialogWindow.SetStatus 操作 StatusMessageTextBlock）
        // spike 版：Footer 自包含的 StatusMessage 便捷属性
        public string StatusMessage
        {
            get => StatusMessageTextBlock?.Text ?? string.Empty;
            set
            {
                if (StatusMessageTextBlock != null)
                {
                    StatusMessageTextBlock.Text = value;
                    StatusMessageTextBlock.IsVisible = !string.IsNullOrEmpty(value);
                }
            }
        }

        // 对照 WPF: ShowSubmitButton（ForkPlusDialogWindow.ShowSubmitButton 操作 SubmitButton.IsVisible）
        public bool ShowSubmitButton
        {
            get => SubmitButton?.IsVisible ?? true;
            set { if (SubmitButton != null) SubmitButton.IsVisible = value; }
        }

        // 对照 WPF: ShowCancelButton（ForkPlusDialogWindow.ShowCancelButton 操作 CancelButton.IsVisible）
        public bool ShowCancelButton
        {
            get => CancelButton?.IsVisible ?? true;
            set { if (CancelButton != null) CancelButton.IsVisible = value; }
        }

        // 对照 WPF: SubmitButtonTitle（ForkPlusDialogWindow.SubmitButtonTitle 操作 SubmitButton.Content）
        public string SubmitButtonTitle
        {
            get => SubmitButton?.Content as string ?? "Submit";
            set { if (SubmitButton != null) SubmitButton.Content = value; }
        }

        // 对照 WPF: CancelButtonTitle（ForkPlusDialogWindow.CancelButtonTitle 操作 CancelButton.Content）
        public string CancelButtonTitle
        {
            get => CancelButton?.Content as string ?? "Cancel";
            set { if (CancelButton != null) CancelButton.Content = value; }
        }

        // ===== task spec 关键 API：SetStatus =====
        // 对照 WPF: ForkPlusDialogWindow.SetStatus(ForkPlusDialogStatus status, string message)
        //   WPF: 操作 StatusMessageTextBlock / StatusImage / BusyIndicator
        // spike 版：Footer 自包含的 SetStatus 方法（与 ForkPlusDialogWindow.SetStatus 逻辑一致）
        public void SetStatus(ForkPlusDialogStatus status, string message)
        {
            if (status == ForkPlusDialogStatus.None)
            {
                ClearStatus();
                return;
            }

            if (StatusMessageTextBlock != null)
            {
                StatusMessageTextBlock.Text = message;
                ToolTip.SetTip(StatusMessageTextBlock, message);
                StatusMessageTextBlock.IsVisible = true;
            }

            if (status == ForkPlusDialogStatus.InProgress)
            {
                if (StatusImage != null) StatusImage.IsVisible = false;
                if (BusyIndicator != null) BusyIndicator.IsVisible = true;
                return;
            }

            if (BusyIndicator != null) BusyIndicator.IsVisible = false;
            if (StatusImage != null)
            {
                StatusImage.IsVisible = true;
                // spike 版：用 emoji 替代 PNG 图标（"⚠" Warning / "✓" Success / "✗" Error）
                StatusImage.Text = status switch
                {
                    ForkPlusDialogStatus.Success => "✓",
                    ForkPlusDialogStatus.Warning => "⚠",
                    ForkPlusDialogStatus.Error => "✗",
                    _ => ""
                };
            }
        }

        // 对照 WPF: ForkPlusDialogWindow.ClearStatus()
        public void ClearStatus()
        {
            if (StatusImage != null) StatusImage.IsVisible = false;
            if (StatusMessageTextBlock != null) StatusMessageTextBlock.IsVisible = false;
            if (BusyIndicator != null) BusyIndicator.IsVisible = false;
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
