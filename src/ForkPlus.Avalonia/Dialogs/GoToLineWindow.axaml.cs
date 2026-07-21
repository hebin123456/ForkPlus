using System;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.1b：Avalonia 版 GoToLineWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GoToLineWindow.xaml.cs（38 行）：
    //   - public partial class GoToLineWindow : ForkPlusDialogWindow
    //   - public int? LineNumber { get; private set; }
    //   - 构造函数：
    //     * base.ShowLogo = false
    //     * base.ShowHeader = false
    //     * base.IsTitleVisible = true
    //     * InitializeComponent()
    //     * base.Title = PreferencesLocalization.Current("Go To Line")
    //     * base.SubmitButtonTitle = PreferencesLocalization.Current("Go")
    //   - protected override void OnSubmit():
    //     * if (int.TryParse(LineNumberTextBox.Text, out var result)) LineNumber = result;
    //       else LineNumber = null;
    //     * CloseWithOk();
    //
    // Avalonia 版差异：
    //   1. 使用 Phase 4.0b spike 模式：构造函数 SetFooter(Footer) 注入 Footer 引用
    //   2. 暂不接入 PreferencesLocalization.Translate（直接字面量英文）
    //   3. SubmitButtonTitle 由基类 SetFooter 后应用到 Footer.SubmitButton.Content
    //
    // 调用方（WPF 版）：
    //   var window = new GoToLineWindow();
    //   if (window.ShowDialog() == true && window.LineNumber.HasValue) { ... }
    //
    // 调用方（Avalonia 版）：
    //   var window = new GoToLineWindow();
    //   var result = await window.ShowDialog<bool?>(owner);
    //   if (result == true && window.LineNumber.HasValue) { ... }
    public partial class GoToLineWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 用户输入的行号（null 表示无效输入）
        public int? LineNumber { get; private set; }

        public GoToLineWindow()
        {
            // 对照 WPF: ShowFooter=true（默认）— 本窗口需要 Submit/Cancel 按钮
            ShowFooter = true;

            InitializeComponent();

            // 注入 Footer 引用（基类借此拿到 SubmitButton/CancelButton 引用）
            SetFooter(Footer);
            // 注入 TitleTextBlock 引用（基类借此同步 DialogTitle → TitleTextBlock.Text）
            SetTitleTextBlock(TitleTextBlock);

            // 对照 WPF: base.Title = PreferencesLocalization.Current("Go To Line")
            Title = "Go To Line";
            DialogTitle = "Go To Line";

            // 对照 WPF: base.SubmitButtonTitle = PreferencesLocalization.Current("Go")
            SubmitButtonTitle = "Go";

            // 默认不显示 Cancel 按钮（WPF 版无 Cancel，只有 Submit）
            // 实际上 WPF ForkPlusDialogFooter 基类默认显示 Cancel；这里与 WPF 行为一致保留 Cancel
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            if (LineNumberTextBox != null &&
                int.TryParse(LineNumberTextBox.Text, out int result))
            {
                LineNumber = result;
            }
            else
            {
                LineNumber = null;
            }
            CloseWithOk();
        }
    }
}
