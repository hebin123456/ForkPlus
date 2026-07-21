using System;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.8b：Avalonia 版 AddCustomRefspecWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AddCustomRefspecWindow.xaml.cs（40 行）：
    //   - public partial class AddCustomRefspecWindow : ForkPlusDialogWindow
    //   - 字段：_remoteName / _localBranchName
    //   - public string OutRefspec { get; private set; }
    //   - 构造函数 (remoteName, localBranchName):
    //     * InitializeComponent()
    //     * DialogTitle = PreferencesLocalization.Current("Custom Remote Branch Name")
    //     * DialogDescription = PreferencesLocalization.Current("Enter custom destination")
    //     * SubmitButtonTitle = PreferencesLocalization.Current("Add")
    //     * RemoteNameTextBlock.Text = remoteName + "/"
    //     * BranchNameTextBox.Text = localBranchName
    //     * BranchNameTextBox.Focus() + SelectAll()
    //   - OnSubmit: OutRefspec = BranchNameTextBox.Text; CloseWithOk();
    //
    // 调用方（WPF 版）：
    //   var window = new AddCustomRefspecWindow(remoteName, localBranchName);
    //   if (window.ShowDialog() == true) { use window.OutRefspec }
    //
    // 调用方（Avalonia 版）：
    //   var window = new AddCustomRefspecWindow(remoteName, localBranchName);
    //   var result = await window.ShowDialog<bool?>(owner);
    //   if (result == true) { use window.OutRefspec }
    public partial class AddCustomRefspecWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly string _remoteName;
        private readonly string _localBranchName;

        // 用户输入的 refspec（OnSubmit 时填充）
        public string OutRefspec { get; private set; }

        public AddCustomRefspecWindow(string remoteName, string localBranchName)
        {
            _remoteName = remoteName;
            _localBranchName = localBranchName;

            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: base.DialogTitle / DialogDescription / SubmitButtonTitle
            string title = Current("Custom Remote Branch Name");
            Title = title;
            DialogTitle = title;
            DialogDescription = Current("Enter custom destination");
            SubmitButtonTitle = Current("Add");

            // 对照 WPF: RemoteNameTextBlock.Text = remoteName + "/"
            RemoteNameTextBlock.Text = (remoteName ?? "") + "/";
            // 对照 WPF: BranchNameTextBox.Text = localBranchName
            BranchNameTextBox.Text = localBranchName ?? "";

            // 对照 WPF: BranchNameTextBox.Focus() + SelectAll()
            // 需要在窗口 Loaded 后调用 Focus（控件尚未完成布局）
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    BranchNameTextBox.Focus();
                    BranchNameTextBox.SelectionStart = 0;
                    BranchNameTextBox.SelectionEnd = BranchNameTextBox.Text?.Length ?? 0;
                }
                catch { /* 控件可能已释放 */ }
            });
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            OutRefspec = BranchNameTextBox.Text;
            CloseWithOk();
        }

        // PreferencesLocalization.Current(text) → ServiceLocator.Localization.Current(text)
        private static string Current(string text)
        {
            var localization = ServiceLocator.Localization;
            return localization != null ? localization.Current(text) : text;
        }
    }
}
