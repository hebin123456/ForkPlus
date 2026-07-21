using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.33b：Avalonia 版 RenameStashWindow（真实迁移版，对照 WPF RenameStashWindow.xaml.cs 97 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/RenameStashWindow.xaml.cs：
    //   - public partial class RenameStashWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / StashRevision _stash
    //   - 属性: Sha? OutResultSha（重命名后的新 stash sha，给调用方使用）
    //   - IsSubmitAllowed override: !IsNullOrWhiteSpace(text) && text != _stash.Message
    //   - GetCommandPreview override: "git stash rename {reflogName} {quotedMessage}"
    //   - OnSubmit: RenameStashGitCommand().Execute(gitModule, reflogName, newMessage, monitor)
    //     → OutResultSha = result.Result → Close(result.ToGitCommandResult())
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Action<GitCommandResult, Sha?>? onCompleted 回调
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBlock
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用 StashNameTextBox
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. StashNameTextBox.SelectAll() → Avalonia TextBox.SelectionStart/SelectionEnd
    //   7. TextChangedEventArgs → Avalonia 同名类型
    public partial class RenameStashWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly StashRevision _stash;
        private readonly Action<GitCommandResult, Sha?>? _onCompleted;

        public Sha? OutResultSha { get; private set; }

        protected override bool IsSubmitAllowed
        {
            get
            {
                ClearStatus();
                string? text = StashNameTextBox.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text != _stash.Message && base.IsSubmitAllowed;
                }
                return false;
            }
        }

        // 构造函数签名与 WPF 不同：用 GitModule + Action 回调替代 RepositoryUserControl
        public RenameStashWindow(
            GitModule gitModule,
            StashRevision stash,
            Action<GitCommandResult, Sha?>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _stash = stash ?? throw new ArgumentNullException(nameof(stash));
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Rename Stash");
            DialogDescription = Translate("Update stash message");
            SubmitButtonTitle = Translate("Rename");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Rename Stash");

            StashNameTextBox.Text = _stash.Message;
            // 对照 WPF: StashNameTextBox.SelectAll();
            StashNameTextBox.SelectionStart = 0;
            StashNameTextBox.SelectionEnd = _stash.Message?.Length ?? 0;

            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            if (_stash == null || string.IsNullOrEmpty(_stash.ReflogName))
            {
                return null;
            }
            string newMessage = StashNameTextBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(newMessage))
            {
                return null;
            }
            string quotedMessage = newMessage.IndexOf(' ') >= 0 ? ("\"" + newMessage + "\"") : newMessage;
            return "git stash rename " + _stash.ReflogName + " " + quotedMessage;
        }

        private void RefreshCommandPreview()
        {
            string? preview = GetCommandPreview();
            CommandPreviewTextBox.Text = preview ?? "";
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            StashRevision stash = _stash;
            string newMessage = StashNameTextBox.Text ?? "";

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Updating message..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(FormatTranslate("Rename stash '{0}'", ...), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult<Sha> renameResult = new RenameStashGitCommand().Execute(
                    _gitModule, stash.ReflogName, newMessage, monitor);
                OutResultSha = renameResult.Result;
                GitCommandResult result = renameResult.ToGitCommandResult();
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result, OutResultSha);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("RenameStashWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: StashName_TextChanged
        public void StashName_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            StashNameTextBox.IsEnabled = false;
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
