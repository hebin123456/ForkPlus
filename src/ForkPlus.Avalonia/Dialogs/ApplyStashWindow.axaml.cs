using System;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.35b：Avalonia 版 ApplyStashWindow（真实迁移版，对照 WPF ApplyStashWindow.xaml.cs 82 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ApplyStashWindow.xaml.cs：
    //   - public partial class ApplyStashWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / StashRevision _stash
    //   - GitPointView.Value = _stash（显示 stash reflog name）
    //   - GetCommandPreview override: "git stash {pop|apply} {reflogName}"
    //   - OnSubmit: ApplyStashGitCommand().Execute(gitModule, reflogName, deleteAfterApply, monitor) → Close(result)
    //   - DeleteStashAfterApplyCheckBox_Changed: 切换 DeleteStashWarningImage 可见性 + RefreshCommandPreview
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Action<GitCommandResult>? onCompleted 回调
    //   3. GitPointView 自定义控件 → spike 版用 TextBlock 显示 reflog name 简化
    //   4. DeleteStashWarningImage (PNG) → spike 版用 emoji TextBlock "⚠" 替代
    //   5. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   6. spike 基类不提供 DisableEditableControls → 手动禁用 CheckBox
    //   7. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   8. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    public partial class ApplyStashWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly StashRevision _stash;
        private readonly Action<GitCommandResult>? _onCompleted;

        // 构造函数签名与 WPF 不同：用 GitModule + Action 回调替代 RepositoryUserControl
        public ApplyStashWindow(
            GitModule gitModule,
            StashRevision stash,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _stash = stash ?? throw new ArgumentNullException(nameof(stash));
            _onCompleted = onCompleted;

            // 对照 WPF: GitPointView.Value = _stash;
            // Avalonia spike: 用 TextBlock 显示 reflog name 简化
            StashNameTextBlock.Text = _stash.ReflogName ?? _stash.Message ?? "(stash)";

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Apply Stash");
            DialogDescription = Translate("Apply changes of the stash to your working directory");
            SubmitButtonTitle = Translate("Apply");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Apply Stash");

            // 对照 WPF: DeleteStashAfterApplyCheckBox.IsChecked = ForkPlusSettings.Default.ApplyStash_DeleteAfterApply;
            DeleteStashAfterApplyCheckBox.IsChecked = ForkPlusSettings.Default.ApplyStash_DeleteAfterApply;
            // 初始状态同步 warning 可见性
            UpdateWarningVisibility();

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_stash == null || string.IsNullOrEmpty(_stash.ReflogName))
            {
                return null;
            }
            bool deleteAfterApply = DeleteStashAfterApplyCheckBox.IsChecked.GetValueOrDefault();
            return "git stash " + (deleteAfterApply ? "pop" : "apply") + " " + _stash.ReflogName;
        }

        private void RefreshCommandPreview()
        {
            CommandPreviewTextBox.Text = GetCommandPreview() ?? "";
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            StashRevision stash = _stash;
            bool deleteAfterApply = DeleteStashAfterApplyCheckBox.IsChecked.GetValueOrDefault();

            // 对照 WPF: ForkPlusSettings.Default.ApplyStash_DeleteAfterApply = deleteAfterApply;
            ForkPlusSettings.Default.ApplyStash_DeleteAfterApply = deleteAfterApply;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Applying stash..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(FormatTranslate("Apply stash '{0}'", ...), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new ApplyStashGitCommand().Execute(
                    _gitModule, stash.ReflogName, deleteAfterApply, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("ApplyStashWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: DeleteStashAfterApplyCheckBox_Changed
        public void DeleteStashAfterApplyCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            UpdateWarningVisibility();
            RefreshCommandPreview();
        }

        // 对照 WPF: DeleteStashWarningImage.Show()/Hide()
        private void UpdateWarningVisibility()
        {
            DeleteStashWarningText.IsVisible = DeleteStashAfterApplyCheckBox.IsChecked.GetValueOrDefault();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            DeleteStashAfterApplyCheckBox.IsEnabled = false;
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
