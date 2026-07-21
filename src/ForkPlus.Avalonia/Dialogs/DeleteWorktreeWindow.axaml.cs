using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.34b：Avalonia 版 DeleteWorktreeWindow（真实迁移版，对照 WPF DeleteWorktreeWindow.xaml.cs 67 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/DeleteWorktreeWindow.xaml.cs：
    //   - public partial class DeleteWorktreeWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / Worktree _worktree
    //   - GetCommandPreview override: "git worktree remove {quotedPath}"
    //   - OnSubmit: RemoveWorktreeGitCommand().Execute(gitModule, worktree.Path, monitor)
    //     → 成功则 MainWindow.Instance.TabManager.CloseTab(worktree.Path) → Close(result)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Action<string>? onCloseTab 回调
    //      （解耦 MainWindow.Instance.TabManager.CloseTab 依赖）
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   4. spike 基类不提供 DisableEditableControls → 空实现（本对话框无可编辑控件）
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. PreferencesLocalization.Current/FormatCurrent → ServiceLocator.Localization.Translate/FormatCurrent
    public partial class DeleteWorktreeWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Worktree _worktree;
        private readonly Action<string>? _onCloseTab;

        // 构造函数签名与 WPF 不同：用 GitModule + Action<string>? onCloseTab 回调替代 RepositoryUserControl
        public DeleteWorktreeWindow(
            GitModule gitModule,
            Worktree worktree,
            Action<string>? onCloseTab = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            // Worktree 是 struct，不可为 null
            _worktree = worktree;
            _onCloseTab = onCloseTab;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            string title = FormatTranslate("Are you sure you want to delete worktree {0}?", _worktree.FriendlyName);
            Title = title;
            DialogTitle = title;
            DialogDescription = FormatTranslate("Do you want to delete worktree {0}?", _worktree.Path);
            SubmitButtonTitle = Translate("Delete");
            CancelButtonTitle = Translate("Cancel");

            // 对照 WPF: RefreshCommandPreview();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (string.IsNullOrEmpty(_worktree.Path))
            {
                return null;
            }
            string path = _worktree.Path;
            string quotedPath = path.IndexOf(' ') >= 0 ? ("\"" + path + "\"") : path;
            return "git worktree remove " + quotedPath;
        }

        private void RefreshCommandPreview()
        {
            CommandPreviewTextBox.Text = GetCommandPreview() ?? "";
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            Worktree worktree = _worktree;
            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting worktree..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(FormatTranslate("Delete worktree '{0}'", ...), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new RemoveWorktreeGitCommand().Execute(_gitModule, worktree.Path, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    if (result.Succeeded)
                    {
                        // 对照 WPF: MainWindow.Instance.TabManager.CloseTab(worktree.Path);
                        // Avalonia spike: 调用注入的回调
                        try
                        {
                            _onCloseTab?.Invoke(worktree.Path);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("DeleteWorktreeWindow onCloseTab callback failed", ex);
                        }
                    }
                    Close(result);
                });
            });
        }

        // spike 版：手动禁用可编辑控件（本对话框无可编辑控件）
        private void DisableEditableControls()
        {
        }

        // 对照 WPF: PreferencesLocalization.Current(text)
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

        // 对照 WPF: PreferencesLocalization.FormatCurrent(text, args)
        private static string FormatTranslate(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}
