using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git.Commands;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 ErrorWindow（真实迁移版，对照 WPF ErrorWindow.xaml.cs 415 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ErrorWindow.xaml.cs：
    //   - public partial class ErrorWindow : ForkPlusDialogWindow
    //   - 字段：RepositoryUserControl _repositoryUserControl, GitCommandError GitCommandError
    //   - 构造函数 ()：DialogTitle="Git Error" / Description="An unexpected error occured..." /
    //     CancelButtonTitle="Close" / ShowSubmitButton=false / ShowWarningIcon=true /
    //     MessageEditor（AvalonEdit TextEditor）配置 EnableHyperlinks=true / WordWrap=true /
    //     LineTransformers.Add(GitOutputColorizer) / RefreshTheme()
    //   - 构造函数 (string message): this() + MessageEditor.Text = message + ScrollToEnd
    //   - 构造函数 (RepositoryUserControl, GitCommandError): this() + 复杂分支按 GitCommandError 子类型
    //     设置 DialogTitle/Description/FirstButton：
    //     * AutomaticMergeFailed → Merge Conflict
    //     * TagMismatch → FirstButton "Force Fetch"
    //     * RepositoryIsLocked → FirstButton "Remove .git/index.lock"
    //     * LfsFileIsLocked → FirstButton "Force Unlock {N} Files" / "Force Unlock"
    //     * PatchDoesNotApply → text += "fork: try to disable 'ignore whitespaces'"
    //     * AuthenticationFailed (Generic) → FirstButton "Credential Manager"
    //     * AuthenticationFailed (GitHubConnectionError) → text + org access 提示
    //     * UnsafeRepository → FirstButton "Mark repository as safe"
    //     * MergeUnrelatedHistory → FirstButton "Merge unrelated history"
    //     * CommitFailed + hooks 存在 → FirstButton "Skip pre-commit hooks and commit"
    //   - FirstButton_Click：分支调用 ForceFetch / RemoveLockIndexFile / ForceUnlock /
    //     AddRepositoryToSafeDirectoriesList / MergeUnrelatedHistory / CommitWithoutHooksAndClose /
    //     OpenCredentialManager
    //   - RefreshTheme：MessageEditor.TextArea.TextView.LinkTextForegroundBrush = TryFindResource("CodeEditorLinkForeground")
    //   - OpenCredentialManager: Process.Start rundll32.exe keymgr.dll
    //   - AddRepositoryToSafeDirectoriesList: AddRepositoryToSafeDirectoriesListGitCommand +
    //     MainWindow.Instance.TabManager.OpenRepository
    //   - CommitWithoutHooksAndClose: JobQueue + CommitGitCommand(noVerify:true) + QuickPush + InvalidateAndRefresh
    //   - ForceFetch / ForceUnlock / MergeUnrelatedHistory: JobQueue + 对应 GitCommand + InvalidateAndRefresh
    //   - ContainsHooks / GetGitConfigHooksPath / HookExists
    //   - Translate: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 构造函数 (Exception? exception = null, GitCommandError? error = null,
    //      string? title = null, string? message = null) 解耦 RepositoryUserControl
    //   3. spike 版省略 FirstButton（依赖 RepositoryUserControl + JobQueue，spike 简化为仅显示错误）
    //   4. spike 版省略 GitOutputColorizer（依赖 AvalonEdit LineTransformer，spike 简化为普通 TextBox）
    //   5. MessageEditor (AvalonEdit TextEditor) → 普通 TextBox（IsReadOnly + AcceptsReturn + WordWrap）
    //   6. 复制按钮用 TopLevel.GetTopLevel(this).Clipboard.SetTextAsync（Avalonia 11 跨平台 API，
    //      Clipboard 在 TopLevel 上不在 Application 上）
    //   7. 折叠详细信息用 Expander 控件（Avalonia 原生 Expander）
    //   8. spike 版 ShowWarningIcon=true 改为在 TitleTextBlock 旁加 emoji "⚠" TextBlock
    //   9. spike 版省略 OpenCredentialManager / ForceFetch / RemoveLockIndexFile / ForceUnlock /
    //      AddRepositoryToSafeDirectoriesList / MergeUnrelatedHistory / CommitWithoutHooksAndClose
    //      （依赖 RepositoryUserControl + JobQueue + MainWindow.Instance，spike 不迁移）
    //  10. spike 版省略 ContainsHooks / GetGitConfigHooksPath / HookExists
    //      （依赖 GitModule + GitConfig，spike 不迁移）
    //  11. spike 版省略 RefreshTheme（依赖 AvalonEdit LineTransformer，spike 简化）
    //  12. PreferencesLocalization → ServiceLocator.Localization.Translate
    //  13. spike 版 GitCommandError 子类型仅用于显示 FriendlyDescription（不分支弹按钮）
    public partial class ErrorWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitCommandError? _gitCommandError;
        private readonly Exception? _exception;

        // 构造函数签名与 WPF 不同：注入 Exception? + GitCommandError? + string? title + string? message
        // 解耦 RepositoryUserControl + Application.Current.Shutdown 依赖
        public ErrorWindow(
            Exception? exception = null,
            GitCommandError? error = null,
            string? title = null,
            string? message = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _exception = exception;
            _gitCommandError = error;

            // 对照 WPF: base.DialogTitle / DialogDescription / CancelButtonTitle / ShowSubmitButton / ShowWarningIcon
            string dialogTitle = title ?? Translate("Git Error");
            string dialogDescription = Translate("An unexpected error occured while performing the git request");
            string messageText = message ?? "";

            // 对照 WPF: MessageEditor 配置（spike 版用普通 TextBox，无需 LineTransformers / Options）
            // 对照 WPF: gitCommandError 子类型分支
            if (error != null)
            {
                // 对照 WPF: if (gitCommandError is GitCommandError.GitError gitError)
                //           text = gitError.FriendlyDescription;
                if (error is GitCommandError.AutomaticMergeFailed)
                {
                    // 对照 WPF: DialogTitle = Translate("Merge Conflict");
                    //           DialogDescription = Translate("Automatic merge failed. Fix conflicts and then commit the result");
                    //           ShowWarningIcon = false;
                    dialogTitle = Translate("Merge Conflict");
                    dialogDescription = Translate("Automatic merge failed. Fix conflicts and then commit the result");
                    WarningIconTextBlock.IsVisible = false;
                    messageText = error.FriendlyDescription ?? "";
                }
                else if (error.FriendlyDescription != null)
                {
                    messageText = error.FriendlyDescription;

                    // 对照 WPF: if (gitCommandError is GitCommandError.PatchDoesNotApply)
                    //           text += "\n" + Translate("fork: try to disable 'ignore whitespaces'");
                    if (error is GitCommandError.PatchDoesNotApply)
                    {
                        messageText += "\n" + Translate("fork: try to disable 'ignore whitespaces'");
                    }

                    // 对照 WPF: AuthenticationFailed (GitHubConnectionError) 分支
                    // spike 版省略 GitHub org 提示（依赖 AccountManager.Accounts，spike 不迁移）
                }
            }
            else if (exception != null)
            {
                // 对照 WPF: text = gitCommandError.FriendlyDescription;
                // spike 版：无 GitCommandError 时，从 Exception 派生 message
                messageText = exception.Message ?? exception.GetType().Name;
            }

            DialogTitle = dialogTitle;
            DialogDescription = dialogDescription;
            CancelButtonTitle = Translate("Close");
            ShowSubmitButton = false;
            Title = dialogTitle;

            // 对照 WPF: MessageEditor.Text = text; MessageEditor.ScrollToEnd();
            MessageTextBox.Text = messageText;

            // 对照 WPF: spike 版 DetailsExpander 显示 Exception 堆栈 + GitCommandError 完整描述
            string details = BuildDetails();
            if (string.IsNullOrEmpty(details))
            {
                DetailsExpander.IsVisible = false;
            }
            else
            {
                DetailsTextBox.Text = details;
            }

            // 对照 WPF: spike 版 Loaded 后滚动到末尾（Avalonia TextBox 无 ScrollToEnd，用 CaretIndex 触发）
            Loaded += (_, _) =>
            {
                MessageTextBox.CaretIndex = (MessageTextBox.Text ?? "").Length;
            };
        }

        // 对照 WPF: spike 版新增 BuildDetails：合并 Exception 堆栈 + GitCommandError 完整描述
        private string BuildDetails()
        {
            StringBuilder sb = new StringBuilder();
            if (_exception != null)
            {
                sb.AppendLine("Exception:")
                  .AppendLine(_exception.ToString())
                  .AppendLine();
            }
            if (_gitCommandError != null)
            {
                sb.AppendLine("GitCommandError:")
                  .AppendLine(_gitCommandError.GetType().FullName ?? _gitCommandError.GetType().Name)
                  .AppendLine(_gitCommandError.FriendlyDescription ?? "");
            }
            return sb.ToString();
        }

        // 对照 WPF: spike 版新增 CopyButton_Click：用 TopLevel.GetTopLevel(this).Clipboard.SetTextAsync
        public async void CopyButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 对照 WPF: ServiceLocator.Clipboard.SetText(MessageEditor.Text)
                // Avalonia 11: Clipboard 在 TopLevel 上，不在 Application 上
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(MessageTextBox.Text ?? "");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to copy error message to clipboard", ex);
            }
        }

        // 对照 WPF: private static string Translate(string text)
        //   return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
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
