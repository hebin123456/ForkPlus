using System;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.43b：Avalonia 版 DiffPopupWindow（真实迁移版，对照 WPF DiffPopupWindow.xaml.cs 118 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/DiffPopupWindow.xaml.cs：
    //   - public partial class DiffPopupWindow : CustomWindow
    //   - 3 个 EventHandler：SelectNext / SelectPrevious / ShowLargeUntrackedChanges
    //   - 字段: FileDiffControl / bool _closing
    //   - 静态 CreateRevisionDiff / CreateCommitDiff 工厂方法（构造 FileDiffControl / CommitFileDiffControl）
    //   - 构造函数：
    //     * PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage)
    //     * HideMinimizeMaximizeButtons = true / ResizeMode = CanResizeWithGrip
    //     * FileDiffControlContainer 装载 FileDiffControl
    //     * KeyDown: Space (close) / PreviewKeyDown: Esc (close) / Up (SelectPrevious) / Down (SelectNext)
    //     * Deactivated → CloseWindow()
    //   - UpdateDiff(GitCommandResult<DiffContent> fileContent) → FileDiffControl.Content + Title
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. FileDiffControl 嵌入式 UserControl（spike 版未迁移）→ 用只读 TextBox 显示 diff 文本
    //   3. UpdateDiff(GitCommandResult<DiffContent>) → spike 版接受 (string filePath, string diffText)
    //   4. SelectNext / SelectPrevious 事件 → spike 版仅保留 EventHandler 声明（不实际驱动 FileDiffControl）
    //   5. KeyDown / PreviewKeyDown → Avalonia KeyDown + KeyEventArgs.Handled
    //   6. Deactivated → Avalonia Window.Deactivated 事件
    //   7. HideMinimizeMaximizeButtons → spike 版跳过（用系统标题栏，spike 不自定义 chrome）
    //   8. ResizeMode = CanResizeWithGrip → CanResize = True（Avalonia 11 标准）
    //   9. ShowLargeUntrackedChanges 事件转发 → spike 版保留事件声明
    //  10. CloseWindow → Close + 防重入 _closing
    public partial class DiffPopupWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: public event EventHandler SelectNext / SelectPrevious / ShowLargeUntrackedChanges
        public event EventHandler SelectNext;
        public event EventHandler SelectPrevious;
        public event EventHandler ShowLargeUntrackedChanges;

        // 对照 WPF: private bool _closing;
        private bool _closing;

        // spike 版：构造函数直接接受 (filePath, diffText)
        // 对照 WPF: 静态 CreateRevisionDiff / CreateCommitDiff 工厂 + (FileDiffControl) 构造函数
        public DiffPopupWindow(string filePath, string diffText)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: PreferencesLocalization.Apply
            string title = Translate("File Preview");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Space: close, Up/Down: move file selection.");

            // 只读对话框：隐藏 Submit 按钮，只保留 Close
            ShowSubmitButton = false;
            CancelButtonTitle = Translate("Close");

            // 对照 WPF: UpdateDiff(fileContent) → Title + Content
            UpdateDiff(filePath, diffText);

            // 对照 WPF: KeyDown / PreviewKeyDown
            KeyDown += DiffPopupWindow_KeyDown;

            // 对照 WPF: Deactivated += CloseWindow
            Deactivated += DiffPopupWindow_Deactivated;
        }

        // 对照 WPF: public void UpdateDiff(GitCommandResult<DiffContent> fileContent)
        // spike 版：直接接受 (filePath, diffText) 替代 GitCommandResult<DiffContent>
        public void UpdateDiff(string filePath, string diffText)
        {
            // 对照 WPF: base.Title = fileContent?.Result?.ChangedFile.Path ?? "File Preview";
            string title = string.IsNullOrEmpty(filePath) ? Translate("File Preview") : filePath;
            Title = title;
            DialogTitle = title;
            DiffContentTextBox.Text = diffText ?? "";
        }

        // 对照 WPF: KeyDown (Space → close) + PreviewKeyDown (Esc → close / Up → SelectPrevious / Down → SelectNext)
        private void DiffPopupWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseWindow();
            }
            else if (e.Key == Key.Space)
            {
                e.Handled = true;
                CloseWindow();
            }
            else if (e.Key == Key.Up)
            {
                e.Handled = true;
                SelectPrevious?.Invoke(this, EventArgs.Empty);
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;
                SelectNext?.Invoke(this, EventArgs.Empty);
            }
        }

        // 对照 WPF: Deactivated += delegate { CloseWindow(); };
        private void DiffPopupWindow_Deactivated(object? sender, EventArgs e)
        {
            CloseWindow();
        }

        // 对照 WPF: private void CloseWindow()
        private void CloseWindow()
        {
            if (_closing)
            {
                return;
            }
            _closing = true;
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to close DiffPopupWindow", ex);
            }
        }

        // 对照 WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }
    }
}
