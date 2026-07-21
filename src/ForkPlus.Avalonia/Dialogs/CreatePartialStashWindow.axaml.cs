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
    // Avalonia 版 CreatePartialStashWindow（对照 WPF CreatePartialStashWindow.xaml.cs 278 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CreatePartialStashWindow.xaml.cs：
    //   - public partial class CreatePartialStashWindow : ForkPlusDialogWindow
    //   - 字段: GitModule _gitModule / bool _aiGenerating
    //   - 构造函数 (GitModule gitModule, ChangedFile[] filesToStash, ChangedFile[] allChangedFiles)
    //   - IsSubmitAllowed: GetFirstSelectedFile() != null
    //   - GetCommandPreview: git stash push [-m "msg"] -- <files>
    //   - OnSubmit: SaveStashGitCommand().Execute(_gitModule, msg, filesToStash.ToArray(), monitor) then Close(result)
    //   - AiGenerateStashNameButton_Click: 读选中文件 diff → OpenAiService.GenerateStashName → 流式写 TextBox
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. ResizeMode.CanResizeWithGrip → CanResize="True"
    //   3. RepositoryUserControl → Action<GitCommandResult>? onCompleted 回调
    //   4. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   5. spike 基类不提供 DisableEditableControls → 手动禁用
    //   6. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   8. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   9. PartialStashFileViewModel (WPF) → Avalonia 工程内同名 stub（省略 FileTypeIcon）
    //  10. Image (FileTypeIcon) → spike 省略不显示
    //  11. ListBox.ScrollIntoView → Avalonia ListBox 同名方法（spike 不调用，避免初始化期布局抖动）
    public partial class CreatePartialStashWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Action<GitCommandResult>? _onCompleted;
        private bool _aiGenerating;

        public CreatePartialStashWindow(
            GitModule gitModule,
            ChangedFile[] filesToStash,
            ChangedFile[] allChangedFiles,
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
            DialogDescription = Translate("Save your local modifications to a new stash. BOTH staged and unstaged changes will be stashed");
            SubmitButtonTitle = Translate("Save Stash");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Save stash");

            // 对照 WPF: StashMessageTextBox.Placeholder = Translate("Stash message (optional)");
            StashMessageTextBox.Watermark = Translate("Stash message (optional)");

            // 对照 WPF: 构建文件列表（去重 + 按 filesToStash 标记初始 Selected + 按 NaturalStringComparer 排序）
            var hashSet = new HashSet<string>();
            var list = new List<PartialStashFileViewModel>();
            if (allChangedFiles != null)
            {
                var filesToStashSet = new HashSet<string>();
                if (filesToStash != null)
                {
                    foreach (var f in filesToStash)
                    {
                        if (f?.Path != null) filesToStashSet.Add(f.Path);
                    }
                }
                foreach (var changedFile in allChangedFiles)
                {
                    if (changedFile == null) continue;
                    string filePath = changedFile.Path;
                    if (filePath == null || !hashSet.Add(filePath)) continue;
                    bool selected = filesToStashSet.Contains(filePath);
                    list.Add(new PartialStashFileViewModel(changedFile, filePath, selected));
                }
            }
            list.Sort((x, y) => NaturalStringComparer.Instance.Compare(x.FilePath, y.FilePath));
            PartialStashListBox.ItemsSource = list;

            // 对照 WPF: AI 按钮可见性 - 仅在 AI 配置完毕时显示
            if (!OpenAiService.IsAiReviewConfigured())
            {
                AiGenerateStashNameButton.IsVisible = false;
            }
            else
            {
                ToolTip.SetTip(AiGenerateStashNameButton, Translate("Use AI to generate a stash message"));
            }

            UpdateSubmitButton();

            // 对照 WPF: base.Dispatcher.Async(delegate { StashMessageTextBox.Focus(); });
            Dispatcher.UIThread.Post(delegate
            {
                StashMessageTextBox.Focus();
            });

            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed => GetFirstSelectedFile() != null;
        protected override bool IsSubmitAllowed => GetFirstSelectedFile() != null && !IsOperationInProgress;

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            var files = new List<string>();
            foreach (var item in PartialStashListBox.Items)
            {
                if (item is PartialStashFileViewModel vm && vm.Selected)
                {
                    files.Add(vm.FilePath);
                }
            }
            if (files.Count == 0)
            {
                return null;
            }
            var parts = new List<string> { "git", "stash", "push" };
            string stashMessage = StashMessageTextBox.Text ?? "";
            if (!string.IsNullOrWhiteSpace(stashMessage))
            {
                string quoted = stashMessage.IndexOf(' ') >= 0 ? ("\"" + stashMessage + "\"") : stashMessage;
                parts.Add("-m");
                parts.Add(quoted);
            }
            parts.Add("--");
            foreach (var f in files)
            {
                parts.Add(f.IndexOf(' ') >= 0 ? ("\"" + f + "\"") : f);
            }
            return string.Join(" ", parts);
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? "";
        }

        // 对照 WPF: AiGenerateStashNameButton_Click
        // AI 生成 stash message：读取选中文件相对 HEAD 的 diff，发送给 AI，流式写入 StashMessageTextBox
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
            // 收集当前选中的文件路径
            var selectedPaths = new List<string>();
            foreach (var item in PartialStashListBox.Items)
            {
                if (item is PartialStashFileViewModel vm && vm.Selected)
                {
                    selectedPaths.Add(vm.FilePath);
                }
            }
            if (selectedPaths.Count == 0)
            {
                var infoBox = new MessageBoxWindow(
                    Translate("AI Generate Stash Name"),
                    Translate("No files selected. Nothing to generate a stash message for."),
                    "OK",
                    showCancelButton: false);
                await infoBox.ShowDialog<bool?>(this);
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
                    // 拉取选中文件相对 HEAD 的 diff（staged + unstaged 都包含）
                    var args = new List<string>
                    {
                        "diff", "--find-renames", "--no-ext-diff", "--no-color", "--submodule=short", "--unified=50", "HEAD", "--"
                    };
                    args.AddRange(selectedPaths);
                    var gitCommand = new GitCommand(args.ToArray());
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
                                Translate("No working directory changes detected for selected files. Nothing to generate a stash message for."),
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

        // 对照 WPF: FileCheckBox_Changed
        public void FileCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: StashMessage_TextChanged
        public void StashMessage_TextChanged(object? sender, TextChangedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            var filesToStash = new List<ChangedFile>();
            foreach (var item in PartialStashListBox.Items)
            {
                if (item is PartialStashFileViewModel vm && vm.Selected)
                {
                    filesToStash.Add(vm.ChangedFile);
                }
            }
            string? stashMessage = string.IsNullOrWhiteSpace(StashMessageTextBox.Text) ? null : StashMessageTextBox.Text;

            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Stashing..."));

            // 对照 WPF: MainWindow.ActiveRepositoryUserControl.JobQueue.Add(Translate("Partial stash"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            var filesArray = filesToStash.ToArray();
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new SaveStashGitCommand().Execute(_gitModule, stashMessage, filesArray, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("CreatePartialStashWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: GetFirstSelectedFile
        private PartialStashFileViewModel? GetFirstSelectedFile()
        {
            foreach (var item in PartialStashListBox.Items)
            {
                if (item is PartialStashFileViewModel vm && vm.Selected)
                {
                    return vm;
                }
            }
            return null;
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            StashMessageTextBox.IsEnabled = false;
            AiGenerateStashNameButton.IsEnabled = false;
            PartialStashListBox.IsEnabled = false;
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
