using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.Utils.Http;

namespace ForkPlus.Avalonia.Dialogs
{
    // Avalonia 版 SaveStashWindow（对照 WPF SaveStashWindow.xaml.cs 197 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/SaveStashWindow.xaml.cs：
    //   - public partial class SaveStashWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule / bool _aiGenerating
    //   - 构造函数 (GitModule gitModule)
    //   - GetCommandPreview: git stash push [-m "msg"] [include untracked flag]
    //   - OnSubmit: SaveStashGitCommand().Execute(_gitModule, msg, stageNewFiles, monitor) then Close(result)
    //   - AiGenerateStashNameButton_Click: 读 workdir diff → OpenAiService.GenerateStashName → 流式写 TextBox
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl → GitModule + Action<GitCommandResult>? onCompleted 回调
    //   3. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   4. spike 基类不提供 DisableEditableControls → 手动禁用
    //   5. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   6. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   7. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   8. AiGenerateStashNameButton.Collapse() → IsVisible = false
    //   9. TextChangedEventArgs 使用 Avalonia 同名类型
    public partial class SaveStashWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Action<GitCommandResult>? _onCompleted;
        private bool _aiGenerating;

        public SaveStashWindow(
            GitModule gitModule,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Save stash");
            DialogDescription = Translate("Save your local changes to a new stash");
            SubmitButtonTitle = Translate("Save Stash");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Save stash");

            // 对照 WPF: StashMessageTextBox.Placeholder = Translate("Stash message (optional)");
            StashMessageTextBox.Watermark = Translate("Stash message (optional)");

            // 对照 WPF: StageNewFilesCheckBox.IsChecked = ForkPlusSettings.Default.SaveStash_StageNewFiles;
            StageNewFilesCheckBox.IsChecked = ForkPlusSettings.Default.SaveStash_StageNewFiles;

            // 对照 WPF: AI 按钮可见性 - 仅在 AI 配置完毕时显示
            if (!OpenAiService.IsAiReviewConfigured())
            {
                AiGenerateStashNameButton.IsVisible = false;
            }
            else
            {
                ToolTip.SetTip(AiGenerateStashNameButton, Translate("Use AI to generate a stash message"));
            }

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string GetCommandPreview()
        {
            bool stageNewFiles = StageNewFilesCheckBox.IsChecked.GetValueOrDefault();
            string stashMessage = StashMessageTextBox.Text ?? "";
            var parts = new List<string> { "git", "stash", "push" };
            if (!string.IsNullOrWhiteSpace(stashMessage))
            {
                parts.Add("-m");
                parts.Add("\"" + stashMessage + "\"");
            }
            if (stageNewFiles)
            {
                parts.Add("--include-untracked");
            }
            return string.Join(" ", parts);
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview();
        }

        // 对照 WPF: AiGenerateStashNameButton_Click
        // AI 生成 stash message：读取工作区 diff，发送给 AI，流式写入 StashMessageTextBox
        private async void AiGenerateStashNameButton_Click(object sender, RoutedEventArgs e)
        {
            if (_aiGenerating)
            {
                return;
            }
            if (!OpenAiService.IsAiReviewConfigured())
            {
                var msgBox = new MessageBoxWindow(
                    Translate("AI Generate Stash Name"),
                    Translate("AI is not configured. Please configure AI review settings in Preferences first."),
                    "OK",
                    showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
                return;
            }
            _aiGenerating = true;
            AiGenerateStashNameButton.IsEnabled = false;
            string originalToolTip = ToolTip.GetTip(AiGenerateStashNameButton) as string;
            ToolTip.SetTip(AiGenerateStashNameButton, Translate("AI is generating..."));
            StashMessageTextBox.Text = "";
            var liveMsg = new StringBuilder();

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            await Task.Run(delegate
            {
                var monitor = new JobMonitor();
                try
                {
                    // 获取工作区全量 diff（staged + unstaged，相对 HEAD）
                    var gitCommand = new GitCommand("diff", "--find-renames", "--no-ext-diff", "--no-color", "--submodule=short", "--unified=50", "HEAD");
                    GitRequestResult gitRequestResult = new GitRequest(_gitModule).Command(gitCommand).Execute();
                    if (gitRequestResult.ExitCode >= 2)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            var errorBox = new MessageBoxWindow(
                                Translate("AI Generate Stash Name"),
                                gitRequestResult.ToGitCommandError().FriendlyDescription,
                                "OK",
                                showCancelButton: false);
                            _ = errorBox.ShowDialog<bool?>(this);
                        });
                        return;
                    }
                    string patch = gitRequestResult.Stdout;
                    if (string.IsNullOrWhiteSpace(patch))
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            var infoBox = new MessageBoxWindow(
                                Translate("AI Generate Stash Name"),
                                Translate("No working directory changes detected. Nothing to generate a stash message for."),
                                "OK",
                                showCancelButton: false);
                            _ = infoBox.ShowDialog<bool?>(this);
                        });
                        return;
                    }
                    OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
                    ServiceResult<OpenAiResponse> response = openAiService.GenerateStashName(patch, monitor, delegate(string chunk)
                    {
                        if (string.IsNullOrEmpty(chunk))
                        {
                            return;
                        }
                        liveMsg.Append(chunk);
                        string snapshot = liveMsg.ToString();
                        Dispatcher.UIThread.Post(delegate
                        {
                            StashMessageTextBox.Text = snapshot;
                        });
                    });
                    Dispatcher.UIThread.Post(delegate
                    {
                        if (monitor.IsCanceled)
                        {
                            return;
                        }
                        if (!response.Succeeded)
                        {
                            var errorBox = new MessageBoxWindow(
                                Translate("AI Generate Stash Name"),
                                response.Error.FriendlyMessage,
                                "OK",
                                showCancelButton: false);
                            _ = errorBox.ShowDialog<bool?>(this);
                        }
                        else
                        {
                            // stash message 应该是单行，去掉可能的换行
                            string msg = response.Result.Message?.Trim() ?? "";
                            msg = msg.Replace("\r", " ").Replace("\n", " ");
                            while (msg.Contains("  "))
                            {
                                msg = msg.Replace("  ", " ");
                            }
                            StashMessageTextBox.Text = msg;
                        }
                    });
                }
                finally
                {
                    // 任务结束（无论成功失败/取消）恢复按钮状态
                    Dispatcher.UIThread.Post(delegate
                    {
                        _aiGenerating = false;
                        AiGenerateStashNameButton.IsEnabled = true;
                        ToolTip.SetTip(AiGenerateStashNameButton,
                            string.IsNullOrEmpty(originalToolTip) ? Translate("Use AI to generate a stash message") : originalToolTip);
                    });
                }
            });
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            bool stageNewFiles = StageNewFilesCheckBox.IsChecked.GetValueOrDefault();
            string? stashMessage = string.IsNullOrWhiteSpace(StashMessageTextBox.Text) ? null : StashMessageTextBox.Text;

            // 对照 WPF: ForkPlusSettings.Default.SaveStash_StageNewFiles = stageNewFiles; ForkPlusSettings.Default.Save();
            ForkPlusSettings.Default.SaveStash_StageNewFiles = stageNewFiles;
            ForkPlusSettings.Default.Save();

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Stashing..."));

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.AddUndoable(...) + JobQueue.Add(...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult<bool> result = new SaveStashGitCommand().Execute(_gitModule, stashMessage, stageNewFiles, monitor);
                GitCommandResult finalResult = result.Succeeded ? GitCommandResult.Success() : GitCommandResult.Failure(result.Error);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(finalResult);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("SaveStashWindow onCompleted callback failed", ex);
                    }
                    Close(finalResult);
                });
            });
        }

        // 对照 WPF: StashMessageTextBox_TextChanged
        public void StashMessageTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: CheckBox_Changed (Checked/Unchecked → IsCheckedChanged)
        public void CheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            StashMessageTextBox.IsEnabled = false;
            StageNewFilesCheckBox.IsEnabled = false;
            AiGenerateStashNameButton.IsEnabled = false;
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
