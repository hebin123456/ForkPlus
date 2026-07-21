using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.0b：Avalonia 版 ForkPlusDialogStatus 枚举（从 WPF 工程迁移）。
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ForkPlusDialogStatus.cs（11 行）：
    //   enum ForkPlusDialogStatus { None, InProgress, Warning, Error, Success }
    public enum ForkPlusDialogStatus
    {
        None,
        InProgress,
        Warning,
        Error,
        Success
    }

    // Phase 4.0b：Avalonia 版 ForkPlusDialogWindow（从 WPF 工程迁移，95 个对话框的基类）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ForkPlusDialogWindow.cs（767 行）：
    //   - public class ForkPlusDialogWindow : CustomWindow
    //   - 静态 Uri：ForkPlusLogo / WarningIcon / ErrorIcon / SuccessIcon（pack URI）
    //   - 受保护字段：Footer / TitleTextBlock / DescriptionTextBlock
    //   - 公共 API：
    //     * DialogTitle / DialogDescription（get/set 同步 TitleTextBlock/DescriptionTextBlock）
    //     * ShowSubmitButton / ShowCancelButton / SubmitButtonTitle / CancelButtonTitle
    //     * ShowWarningIcon（添加/移除 Warning Icon Image）
    //     * SetStatus(status, message) / ClearStatus
    //     * DisableEditableControls / EnableEditableControls
    //     * GitResult (public get)
    //     * IsOperationInProgress (public get)
    //   - 受保护 virtual：OnSubmit / OnCancel / IsSubmitAllowed / ApplyAutomaticLocalization /
    //     GetCommandPreview（子类重写）
    //   - 受保护方法：Close(GitCommandResult) / CloseWithOk / UpdateSubmitButton /
    //     RefreshCommandPreview
    //   - InitializeDialogChrome()：动态构造 Header + Logo + Footer + CommandPreview
    //     到 Content Grid
    //   - 订阅 NotificationCenter.ApplicationThemeChanged
    //   - OnKeyDown：ESC 触发 OnCancel
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 版不动态构造 chrome（WPF 的 InitializeDialogChrome 通过 VisualTreeHelper
    //      操作 Grid.Children，Avalonia 习惯用 axaml 模板）。子类自己 axaml 中包含
    //      Header + Footer，基类只提供 API + 状态管理。
    //   2. 跳过 GetCommandPreview / RefreshCommandPreview / AddCommandPreview（WPF 在运行时
    //      往 Grid 里塞命令预览区块，Avalonia spike 版子类自己在 axaml 中放 TextBlock，
    //      Phase 4.0c 再统一抽 ControlTemplate）
    //   3. 跳过 AddForkPlusLogo / AddWarningIcon（WPF 动态塞 Image 到 Grid，
    //      Avalonia spike 版子类自己在 axaml 中放 Image）
    //   4. 跳过 DisableEditableControls / EnableEditableControls（依赖 VisualTreeHelper.EnumerateChildren，
    //      Avalonia 用 this.VisualDescendants 替代；spike 版留 Phase 4.0c）
    //   5. 跳过 NotificationCenter.ApplicationThemeChanged 订阅
    //      （NotificationCenter 在 WPF 工程，Avalonia 工程不可访问；
    //      Phase 0 抽 INotificationService 后再接入）
    //   6. 跳过 RefreshWindowSize（依赖 ForkPlusSettings.Default.LayoutScaling，
    //      WPF-only Settings，spike 不接入）
    //   7. 跳过 ApplyAutomaticLocalization（依赖 PreferencesLocalization，
    //      spike 不接入，子类自己 Translate）
    //   8. ESC 关闭：Avalonia 用 KeyDown + Key.Escape + Handled
    //   9. CloseWithOk：WPF 用 DialogResult=true（仅模态有效），Avalonia 用 Close()
    //      返回值用 result parameter（spike 简化为直接 Close）
    //  10. 跳过 IsWindowModal（ComponentDispatcher.IsThreadModal，WPF-only）
    //
    // 本 spike 版保留的核心 API（供 95 个子类继承）：
    //   - DialogTitle / DialogDescription（同步子类 axaml 中的 TitleTextBlock/DescriptionTextBlock）
    //   - ShowSubmitButton / ShowCancelButton / SubmitButtonTitle / CancelButtonTitle
    //     （操作子类 axaml 中的 Footer）
    //   - SetStatus / ClearStatus（操作子类 axaml 中的 Footer.StatusMessage/StatusImage/BusyIndicator）
    //   - OnSubmit / OnCancel / IsSubmitAllowed（virtual，子类重写）
    //   - CloseWithOk / UpdateSubmitButton
    //   - ESC 触发 OnCancel
    //
    // 子类使用范式（spike）：
    //   1. axaml 中根元素：<dialogs:ForkPlusDialogWindow ...>
    //   2. 内容 Grid 3 行：Header / Content / Footer
    //   3. Header 行放 TitleTextBlock + DescriptionTextBlock + Logo Image
    //   4. Footer 行放 <dialogs:ForkPlusDialogFooter Name="Footer" />
    //   5. code-behind 构造函数中：base.SetFooter(Footer) 让基类拿到引用
    //
    // 本 spike 版验证：
    //   - 子类继承 ForkPlusDialogWindow 可使用所有受保护 API
    //   - SetStatus 可控制 Footer 的 StatusMessage/StatusImage/BusyIndicator
    //   - ESC 可触发 OnCancel
    //   - Submit/Cancel 按钮点击可触发对应 virtual 方法
    public class ForkPlusDialogWindow : CustomWindow
    {
        // spike 版：Footer 引用由子类通过 SetFooter() 注入
        // （WPF 是基类动态创建 Footer，Avalonia spike 版让子类在 axaml 中声明）
        private ForkPlusDialogFooter? _footer;
        private TextBlock? _titleTextBlock;
        private TextBlock? _descriptionTextBlock;

        // 对照 WPF: public bool IsOperationInProgress { get; private set; }
        public bool IsOperationInProgress { get; private set; }

        // 对照 WPF: protected new bool ShowHeader { get; set; } = true;
        // spike 版用 StyledProperty（继承自 CustomWindow）

        // 对照 WPF: protected bool ShowLogo { get; set; } = true;
        // spike 版不强制 Logo，子类自己决定 axaml 中是否放 Image

        // 对照 WPF: protected bool ShowFooter { get; set; } = true;
        // spike 版：true 表示子类已注入 Footer，false 表示无 Footer（ESC 不触发 OnCancel）
        public bool ShowFooter { get; set; } = true;

        // 对照 WPF: public bool ShowWarningIcon
        // spike 版简化为属性，子类自己 axaml 中放 Warning Image 并绑定此属性
        public bool ShowWarningIcon { get; set; }

        // 对照 WPF: protected TextBlock TitleTextBlock { get; private set; }
        protected TextBlock? TitleTextBlock => _titleTextBlock;
        protected TextBlock? DescriptionTextBlock => _descriptionTextBlock;
        protected ForkPlusDialogFooter? Footer => _footer;

        // 对照 WPF: protected string DialogTitle
        // spike 版：若子类已注入 TitleTextBlock 则同步 .Text，否则仅存 _pendingDialogTitle
        private string? _pendingDialogTitle;
        public string DialogTitle
        {
            get => _titleTextBlock?.Text ?? _pendingDialogTitle ?? "";
            set
            {
                _pendingDialogTitle = value;
                if (_titleTextBlock != null) _titleTextBlock.Text = value;
                Title = value; // 同步 Window.Title（任务栏文字）
            }
        }

        // 对照 WPF: protected string DialogDescription
        private string? _pendingDialogDescription;
        public string DialogDescription
        {
            get => _descriptionTextBlock?.Text ?? _pendingDialogDescription ?? "";
            set
            {
                _pendingDialogDescription = value;
                if (_descriptionTextBlock != null) _descriptionTextBlock.Text = value;
            }
        }

        // 对照 WPF: protected bool ShowSubmitButton
        private bool? _pendingShowSubmitButton;
        public bool ShowSubmitButton
        {
            get => _footer?.SubmitButton.IsVisible ?? _pendingShowSubmitButton ?? true;
            set
            {
                _pendingShowSubmitButton = value;
                if (_footer?.SubmitButton != null) _footer.SubmitButton.IsVisible = value;
            }
        }

        // 对照 WPF: protected string SubmitButtonTitle
        private string? _pendingSubmitButtonTitle;
        public string SubmitButtonTitle
        {
            get => (_footer?.SubmitButton.Content as string) ?? _pendingSubmitButtonTitle ?? "Submit";
            set
            {
                _pendingSubmitButtonTitle = value;
                if (_footer?.SubmitButton != null) _footer.SubmitButton.Content = value;
            }
        }

        // 对照 WPF: protected bool ShowCancelButton
        private bool? _pendingShowCancelButton;
        public bool ShowCancelButton
        {
            get => _footer?.CancelButton.IsVisible ?? _pendingShowCancelButton ?? true;
            set
            {
                _pendingShowCancelButton = value;
                if (_footer?.CancelButton != null) _footer.CancelButton.IsVisible = value;
            }
        }

        // 对照 WPF: protected string CancelButtonTitle
        private string? _pendingCancelButtonTitle;
        public string CancelButtonTitle
        {
            get => (_footer?.CancelButton.Content as string) ?? _pendingCancelButtonTitle ?? "Cancel";
            set
            {
                _pendingCancelButtonTitle = value;
                if (_footer?.CancelButton != null) _footer.CancelButton.Content = value;
            }
        }

        // 对照 WPF: protected virtual bool IsSubmitAllowed => !IsOperationInProgress
        protected virtual bool IsSubmitAllowed => !IsOperationInProgress;

        public ForkPlusDialogWindow()
        {
            // spike 版：不订阅 NotificationCenter，不动态构造 chrome
            // 子类在 axaml 中自己组织 Header + Footer，通过 SetFooter/SetTitleTextBlock 注入
            KeyDown += ForkPlusDialogWindow_KeyDown;
        }

        // 子类在 InitializeComponent 后调用，注入 Footer 引用
        // 对照 WPF: ForkPlusDialogWindow 动态 AddFooter 后赋值 Footer 字段
        protected void SetFooter(ForkPlusDialogFooter footer)
        {
            _footer = footer ?? throw new ArgumentNullException(nameof(footer));
            _footer.Submit += (_, _) => OnSubmit();
            _footer.Cancel += (_, _) => OnCancel();

            // 应用 pending 值（在 SetFooter 之前设置的 ShowSubmitButton 等）
            if (_pendingShowSubmitButton.HasValue) ShowSubmitButton = _pendingShowSubmitButton.Value;
            if (_pendingShowCancelButton.HasValue) ShowCancelButton = _pendingShowCancelButton.Value;
            if (_pendingSubmitButtonTitle != null) SubmitButtonTitle = _pendingSubmitButtonTitle;
            if (_pendingCancelButtonTitle != null) CancelButtonTitle = _pendingCancelButtonTitle;

            UpdateSubmitButton();
        }

        // 子类在 InitializeComponent 后调用，注入 TitleTextBlock 引用
        protected void SetTitleTextBlock(TextBlock textBlock)
        {
            _titleTextBlock = textBlock ?? throw new ArgumentNullException(nameof(textBlock));
            if (_pendingDialogTitle != null) DialogTitle = _pendingDialogTitle;
        }

        // 子类在 InitializeComponent 后调用，注入 DescriptionTextBlock 引用
        protected void SetDescriptionTextBlock(TextBlock textBlock)
        {
            _descriptionTextBlock = textBlock ?? throw new ArgumentNullException(nameof(textBlock));
            if (_pendingDialogDescription != null) DialogDescription = _pendingDialogDescription;
        }

        // 对照 WPF: public void SetStatus(ForkPlusDialogStatus status, string message)
        public void SetStatus(ForkPlusDialogStatus status, string message)
        {
            IsOperationInProgress = status == ForkPlusDialogStatus.InProgress;
            if (status == ForkPlusDialogStatus.None || _footer == null)
            {
                ClearStatus();
                return;
            }

            // spike 版：StatusMessageTextBlock 直接显示原文（WPF 调 PreferencesLocalization.Translate）
            // Phase 0 抽 ILocalizationService 后再接入
            _footer.StatusMessageTextBlock.Text = message;
            _footer.StatusMessageTextBlock.ToolTip = message;
            _footer.StatusMessageTextBlock.IsVisible = true;

            if (status == ForkPlusDialogStatus.InProgress)
            {
                _footer.StatusImage.IsVisible = false;
                _footer.BusyIndicator.IsVisible = true;
                return;
            }

            _footer.BusyIndicator.IsVisible = false;
            _footer.StatusImage.IsVisible = true;
            // spike 版：用 emoji 替代 PNG 图标（"⚠" Warning / "✓" Success / "✗" Error）
            _footer.StatusImage.Text = status switch
            {
                ForkPlusDialogStatus.Success => "✓",
                ForkPlusDialogStatus.Warning => "⚠",
                ForkPlusDialogStatus.Error => "✗",
                _ => ""
            };
        }

        // 对照 WPF: public void ClearStatus()
        public void ClearStatus()
        {
            if (_footer == null) return;
            _footer.StatusImage.IsVisible = false;
            _footer.StatusMessageTextBlock.IsVisible = false;
            _footer.BusyIndicator.IsVisible = false;
        }

        // 对照 WPF: protected virtual void OnCancel()
        protected virtual void OnCancel()
        {
            if (IsVisible) Close();
        }

        // 对照 WPF: protected virtual void OnSubmit()
        protected virtual void OnSubmit()
        {
            CloseWithOk();
        }

        // 对照 WPF: protected void CloseWithOk()
        protected void CloseWithOk()
        {
            if (IsVisible) Close();
        }

        // 对照 WPF: protected void UpdateSubmitButton()
        protected void UpdateSubmitButton()
        {
            if (_footer?.SubmitButton != null)
            {
                _footer.SubmitButton.IsEnabled = IsSubmitAllowed;
            }
        }

        // 对照 WPF: protected override void OnKeyDown(KeyEventArgs e)
        // ESC 触发 OnCancel（仅当 ShowFooter && ShowCancelButton）
        private void ForkPlusDialogWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (ShowFooter && ShowCancelButton && e.Key == Key.Escape)
            {
                OnCancel();
                e.Handled = true;
            }
        }
    }
}
