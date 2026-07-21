using System;
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
    // Phase 4.32b：Avalonia 版 SaveSnapshotWindow（真实迁移版，对照 WPF SaveSnapshotWindow.xaml.cs 96 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/SaveSnapshotWindow.xaml.cs：
    //   - public partial class SaveSnapshotWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl
    //   - 构造函数 (RepositoryUserControl repositoryUserControl)
    //   - GetCommandPreview override: "git stash push [--include-untracked] [-m \"<msg>\"]"
    //   - OnSubmit: SaveWorkingDirectoryAsStashGitCommand().Execute(gitModule, msg, stageNewFiles, sourceString, monitor)
    //     → Close(result)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + RepositoryReferences? + Action<GitCommandResult>? onCompleted 回调
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 StashMessageTextBox + StageNewFilesCheckBox
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   7. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   8. TextChangedEventArgs → Avalonia 同名类型
    public partial class SaveSnapshotWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly RepositoryReferences? _references;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + RepositoryReferences? + Action 回调替代 RepositoryUserControl
        // （RepositoryUserControl 在 Avalonia 端尚未迁移，spike 版解耦）
        public SaveSnapshotWindow(
            GitModule gitModule,
            RepositoryReferences? references = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _references = references;
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Save snapshot");
            DialogDescription = Translate("Save your local changes to a new stash, but keep them in the working directory");
            SubmitButtonTitle = Translate("Save Snapshot");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Save snapshot");

            // 对照 WPF: StashMessageTextBox.Placeholder = Translate("Stash message (optional)");
            StashMessageTextBox.Watermark = Translate("Stash message (optional)");
            // 对照 WPF: StageNewFilesCheckBox.IsChecked = ForkPlusSettings.Default.SaveStash_StageNewFiles;
            StageNewFilesCheckBox.IsChecked = ForkPlusSettings.Default.SaveStash_StageNewFiles;

            // 对照 WPF: RefreshCommandPreview();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            var parts = new System.Collections.Generic.List<string> { "git", "stash", "push" };
            if (StageNewFilesCheckBox.IsChecked.GetValueOrDefault())
            {
                parts.Add("--include-untracked");
            }
            string message = StashMessageTextBox.Text ?? "";
            if (!string.IsNullOrWhiteSpace(message))
            {
                parts.Add("-m");
                parts.Add(message.Contains(" ") ? "\"" + message + "\"" : message);
            }
            return string.Join(" ", parts);
        }

        private void RefreshCommandPreview()
        {
            CommandPreviewTextBox.Text = GetCommandPreview();
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            if (_references == null)
            {
                return;
            }
            bool stageNewFiles = StageNewFilesCheckBox.IsChecked.GetValueOrDefault();
            string? stashMessage = string.IsNullOrWhiteSpace(StashMessageTextBox.Text) ? null : StashMessageTextBox.Text;
            string sourceString = _references.ActiveBranch?.Name ?? _references.HeadSha?.ToAbbreviatedString() ?? "";

            // 对照 WPF: ForkPlusSettings.Default.SaveStash_StageNewFiles = stageNewFiles; ForkPlusSettings.Default.Save();
            ForkPlusSettings.Default.SaveStash_StageNewFiles = stageNewFiles;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Stashing..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(Translate("Stash snapshot"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new SaveWorkingDirectoryAsStashGitCommand().Execute(
                    _gitModule, stashMessage, stageNewFiles, sourceString, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("SaveSnapshotWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: StashMessageTextBox_TextChanged
        public void StashMessageTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: StageNewFilesCheckBox_Changed（WPF 用 Checked/Unchecked 两个事件，Avalonia 用 IsCheckedChanged）
        public void StageNewFilesCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            StashMessageTextBox.IsEnabled = false;
            StageNewFilesCheckBox.IsEnabled = false;
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
