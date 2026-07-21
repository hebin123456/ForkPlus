using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.40b：Avalonia 版 AddGitIgnorePatternWindow（真实迁移版，对照 WPF AddGitIgnorePatternWindow.xaml.cs 113 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AddGitIgnorePatternWindow.xaml.cs：
    //   - public partial class AddGitIgnorePatternWindow : ForkPlusDialogWindow
    //   - 字段: DelayedAction<string> _updatePreviewAction / GitModule _gitModule / string _initialPattern
    //   - 构造函数 (GitModule gitModule, string initialPattern)
    //   - IsSubmitAllowed: PatternTextBox.Text 非空白
    //   - GetCommandPreview: "# .gitignore\ngit rm --cached -r ."（与 IgnoreFilesGitCommand 对应）
    //   - OnSubmit: IgnoreFilesGitCommand().Execute(_gitModule, pattern, monitor) → Close(result)
    //   - PatternTextBox_TextChanged: 延迟 0.3s 后调 UpdatePreview + RefreshCommandPreview
    //   - UpdatePreview: GetFilesToIgnoreGitCommand().Execute(_gitModule, patterns) → 显示匹配文件列表
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   3. spike 基类不提供 DisableEditableControls → 手动禁用 PatternTextBox
    //   4. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   5. MainWindow.ActiveRepositoryUserControl.JobQueue.Add → 注入 Action<GitCommandResult>? onCompleted 回调
    //   6. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   7. Task.ContinueWith + TaskScheduler.FromCurrentSynchronizationContext → Task.Run + Dispatcher.UIThread.Post
    //   8. TextChangedEventArgs → Avalonia 同名类型
    //   9. DelayedAction 回调由 ServiceLocator.Dispatcher.Post 切回 UI 线程，无需改写
    public partial class AddGitIgnorePatternWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly DelayedAction<string> _updatePreviewAction;
        private readonly GitModule _gitModule;
        private readonly string _initialPattern;
        private readonly Action<GitCommandResult> _onCompleted;

        // 构造函数签名与 WPF 不同：增加 Action<GitCommandResult>? onCompleted 回调
        // （MainWindow.ActiveRepositoryUserControl.JobQueue.Add 在 Avalonia 端尚未迁移，spike 版解耦）
        public AddGitIgnorePatternWindow(
            GitModule gitModule,
            string initialPattern,
            Action<GitCommandResult> onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _initialPattern = initialPattern;
            _onCompleted = onCompleted;

            // 对照 WPF: _updatePreviewAction = new DelayedAction<string>(UpdatePreview, 0.3);
            _updatePreviewAction = new DelayedAction<string>(UpdatePreview, 0.3);

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Add Pattern to .gitignore");
            DialogDescription = Translate("A gitignore file specifies intentionally untracked files that Git should ignore. Files already tracked by Git will be untracked.");
            SubmitButtonTitle = Translate("Add to .gitignore");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Add Pattern to .gitignore");

            // 对照 WPF: PatternLabelTextBlock.Text / PreviewLabelTextBlock.Text
            PatternLabelTextBlock.Text = Translate("(one pattern per line)");
            PreviewLabelTextBlock.Text = Translate("0 files match");

            // 对照 WPF: PatternTextBox.Text = _initialPattern + _updatePreviewAction.InvokeNow(_initialPattern)
            PatternTextBox.Text = _initialPattern;
            _updatePreviewAction.InvokeNow(_initialPattern);

            // 对照 WPF: RefreshCommandPreview（InitializeComponent 后第一次刷新）
            // WPF 注释：InitializeComponent 期间 AddCommandPreview 已执行，但此时 PatternTextBox 尚未赋值，
            // 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed => !string.IsNullOrWhiteSpace(PatternTextBox.Text);

        // 对照 WPF: protected override string GetCommandPreview()
        // 与 IgnoreFilesGitCommand 对应：把 pattern 写入 .gitignore，并对已跟踪文件执行 git rm --cached -r .
        private string GetCommandPreview()
        {
            string text = PatternTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            return "# .gitignore\ngit rm --cached -r .";
        }

        private void RefreshCommandPreview()
        {
            CommandPreviewTextBox.Text = GetCommandPreview();
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            string pattern = PatternTextBox.Text.Trim();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Adding files to .gitignore..."));

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("Add files to .gitignore"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new IgnoreFilesGitCommand().Execute(_gitModule, pattern, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("AddGitIgnorePatternWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: PatternTextBox_TextChanged
        public void PatternTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            _updatePreviewAction.InvokeWithDelay(PatternTextBox.Text);
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: UpdatePreview
        // WPF 用 Task + ContinueWith + TaskScheduler.FromCurrentSynchronizationContext
        // Avalonia 用 Task.Run + Dispatcher.UIThread.Post
        private void UpdatePreview(string pattern)
        {
            string[] patterns = pattern.Trim().Split(Consts.Chars.NewLine);
            Task.Run(delegate
            {
                GitCommandResult<string[]> taskResult = new GetFilesToIgnoreGitCommand().Execute(_gitModule, patterns);
                Dispatcher.UIThread.Post(delegate
                {
                    // 对照 WPF: if (PatternTextBox.Text == pattern) 防止旧任务覆盖新结果
                    if (PatternTextBox.Text != pattern)
                    {
                        return;
                    }
                    string text = "";
                    string text2 = "";
                    if (taskResult.Succeeded)
                    {
                        string[] result2 = taskResult.Result;
                        text = string.Join("\n", result2);
                        text2 = (result2.Length == 1)
                            ? Translate("1 file matches")
                            : FormatTranslate("{0} files match", result2.Length);
                    }
                    else
                    {
                        text = "";
                        text2 = Translate("0 files match");
                    }
                    PreviewTextBox.Text = text;
                    PreviewLabelTextBlock.Text = text2;
                    SetStatus(ForkPlusDialogStatus.None, "");
                });
            });
            SetStatus(ForkPlusDialogStatus.InProgress, "");
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            PatternTextBox.IsEnabled = false;
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
