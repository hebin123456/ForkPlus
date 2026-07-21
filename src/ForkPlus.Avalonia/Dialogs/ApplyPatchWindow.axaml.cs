using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Avalonia 版 ApplyPatchWindow（对照 WPF ApplyPatchWindow.xaml.cs 207 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ApplyPatchWindow.xaml.cs：
    //   - public partial class ApplyPatchWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / byte[] _patchData / bool _patchContainsCommitHeader
    //   - 两个构造函数：
    //     * (RepositoryUserControl, string patchPath) - 文件路径模式（显示 Location 行 + Browse 按钮）
    //     * (RepositoryUserControl, byte[] patchData) - 剪贴板模式（隐藏 Location 行 + Browse 按钮）
    //   - IsSubmitAllowed: _patchData != null || File.Exists(PathTextBox.Text.Trim())
    //   - GetCommandPreview: git am|apply [path]
    //   - OnSubmit: AmGitCommand/ApplyPatchGitCommand().Execute(gitModule, patchData|filePath, monitor) → Close(result)
    //   - BrowseButton_Click: OpenDialog.SelectFile → PathTextBox.Text
    //   - TestForConflicts: ApplyPatchTestGitCommand.Execute → SetStatus(Success|Warning)
    //   - RefreshCreateCommitsCheckBoxVisibility: 根据 _patchContainsCommitHeader 切换 CheckBox 可见性
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl → GitModule + Func<string>? defaultSourceDirProvider + Action<GitCommandResult>? onCompleted 回调
    //   3. RepositoryManager.Instance.DefaultSourceDir() → 注入 Func<string>? defaultSourceDirProvider
    //   4. OpenDialog.SelectFile → TopLevel.StorageProvider.OpenFilePickerAsync
    //   5. spike 基类不提供 GetCommandPreview/RefreshCommandPreview → 自行维护 CommandPreviewTextBox
    //   6. spike 基类不提供 DisableEditableControls → 手动禁用
    //   7. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   8. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    //   9. Collapse()/Show() → IsVisible = false/true
    //  10. AmGitCommand/ApplyPatchGitCommand 为 internal，依赖 ForkPlus.Core 的 InternalsVisibleTo("ForkPlus.Avalonia")
    public partial class ApplyPatchWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Func<string>? _defaultSourceDirProvider;
        private readonly Action<GitCommandResult>? _onCompleted;

        private readonly byte[]? _patchData;

        private bool _patchContainsCommitHeader;

        // 构造函数 1：文件路径模式（显示 Location 行 + Browse 按钮）
        public ApplyPatchWindow(
            GitModule gitModule,
            string patchPath,
            Func<string>? defaultSourceDirProvider = null,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _defaultSourceDirProvider = defaultSourceDirProvider;
            _onCompleted = onCompleted;
            _patchData = null;

            // 对照 WPF: _patchContainsCommitHeader = PatchContainsCommitHeader(patchPath);
            _patchContainsCommitHeader = PatchContainsCommitHeader(patchPath);

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Apply Patch");
            DialogDescription = Translate("Apply Patch");
            SubmitButtonTitle = Translate("Apply");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Apply Patch");

            // 对照 WPF: PathTextBox.Text = patchPath; PathTextBox.SelectAll();
            PathTextBox.Text = patchPath ?? "";
            PathTextBox.SelectionStart = 0;
            PathTextBox.SelectionEnd = PathTextBox.Text?.Length ?? 0;

            RefreshCreateCommitsCheckBoxVisibility();
            TestForConflicts();
            RefreshCommandPreview();
        }

        // 构造函数 2：剪贴板模式（隐藏 Location 行 + Browse 按钮）
        public ApplyPatchWindow(
            GitModule gitModule,
            byte[] patchData,
            Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _onCompleted = onCompleted;
            _patchData = patchData ?? throw new ArgumentNullException(nameof(patchData));

            // 对照 WPF: string @string = Encoding.UTF8.GetString(patchData); _patchContainsCommitHeader = @string.StartsWith("From ");
            string @string = Encoding.UTF8.GetString(patchData);
            _patchContainsCommitHeader = @string.StartsWith("From ");

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Apply Patch");
            DialogDescription = Translate("Apply patch from clipboard");
            SubmitButtonTitle = Translate("Apply");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Apply Patch");

            // 对照 WPF: LocationLabel.Collapse(); PathTextBox.Collapse(); BrowseButton.Collapse();
            LocationLabel.IsVisible = false;
            PathTextBox.IsVisible = false;
            BrowseButton.IsVisible = false;

            RefreshCreateCommitsCheckBoxVisibility();
            TestForConflicts();
            RefreshCommandPreview();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (IsOperationInProgress) return false;
                if (_patchData != null)
                {
                    return true;
                }
                return File.Exists(PathTextBox.Text.Trim());
            }
        }

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            bool createCommits = CreateCommitsCheckBox.IsVisible && CreateCommitsCheckBox.IsChecked.GetValueOrDefault();
            string command = createCommits ? "git am" : "git apply";
            if (_patchData != null)
            {
                return command;
            }
            string filePath = (PathTextBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }
            string quotedPath = filePath.IndexOf(' ') >= 0 ? ("\"" + filePath + "\"") : filePath;
            return command + " " + quotedPath;
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? "";
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            GitModule gitModule = _gitModule;
            bool createCommits = CreateCommitsCheckBox.IsVisible && CreateCommitsCheckBox.IsChecked.GetValueOrDefault();
            DisableEditableControls();
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Applying patch..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(Translate("Apply patch"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            if (_patchData != null)
            {
                var patchData = _patchData;
                Task.Run(delegate
                {
                    JobMonitor monitor = new JobMonitor();
                    GitCommandResult result = createCommits
                        ? new AmGitCommand().Execute(gitModule, patchData, monitor)
                        : new ApplyPatchGitCommand().Execute(gitModule, patchData, monitor);
                    Dispatcher.UIThread.Post(delegate
                    {
                        try
                        {
                            _onCompleted?.Invoke(result);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("ApplyPatchWindow onCompleted callback failed", ex);
                        }
                        Close(result);
                    });
                });
                return;
            }
            string filePath = (PathTextBox.Text ?? "").Trim();
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = createCommits
                    ? new AmGitCommand().Execute(gitModule, filePath, monitor)
                    : new ApplyPatchGitCommand().Execute(gitModule, filePath, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("ApplyPatchWindow onCompleted callback failed", ex);
                    }
                    Close(result);
                });
            });
        }

        // 对照 WPF: BrowseButton_Click
        private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            string initialDirectory = ForkPlusSettings.Default.RecentPatchDirectory
                ?? _defaultSourceDirProvider?.Invoke()
                ?? Environment.ExpandEnvironmentVariables("%userprofile%");

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = Translate("Select patch"),
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Git Patch")
                    {
                        Patterns = new List<string> { "*.patch", "*.diff", "*.*" }
                    }
                }
            };

            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                try
                {
                    var uri = new Uri(Path.GetFullPath(initialDirectory));
                    var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(uri);
                    if (folder != null) options.SuggestedStartLocation = folder;
                }
                catch { }
            }

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (result != null && result.Count > 0)
            {
                string filePath = result[0].Path.LocalPath;
                PathTextBox.Text = filePath;
                PathTextBox.Focus();
                PathTextBox.SelectionStart = 0;
                PathTextBox.SelectionEnd = filePath.Length;
                _patchContainsCommitHeader = PatchContainsCommitHeader(filePath);
                RefreshCreateCommitsCheckBoxVisibility();
                UpdateSubmitButton();
                TestForConflicts();
                RefreshCommandPreview();
            }
        }

        // 对照 WPF: PathTextBox_TextChanged
        public void PathTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            _patchContainsCommitHeader = PatchContainsCommitHeader((PathTextBox.Text ?? "").Trim());
            RefreshCreateCommitsCheckBoxVisibility();
            UpdateSubmitButton();
            TestForConflicts();
            RefreshCommandPreview();
        }

        // 对照 WPF: CreateCommitsCheckBox_Changed
        public void CreateCommitsCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            RefreshCommandPreview();
        }

        // 对照 WPF: TestForConflicts
        private void TestForConflicts()
        {
            GitCommandResult<ApplyPatchTestGitCommand.TestResult> gitCommandResult;
            if (_patchData != null)
            {
                gitCommandResult = new ApplyPatchTestGitCommand().Execute(_gitModule, _patchData);
            }
            else
            {
                string text = (PathTextBox.Text ?? "").Trim();
                if (!File.Exists(text))
                {
                    return;
                }
                gitCommandResult = new ApplyPatchTestGitCommand().Execute(_gitModule, text);
            }
            if (gitCommandResult.Succeeded)
            {
                if (gitCommandResult.Result == ApplyPatchTestGitCommand.TestResult.Success)
                {
                    SetStatus(ForkPlusDialogStatus.Success, Translate("Patch can be applied without conflicts"));
                }
                else if (gitCommandResult.Result == ApplyPatchTestGitCommand.TestResult.Conflict)
                {
                    SetStatus(ForkPlusDialogStatus.Warning, Translate("Patch will cause conflicts"));
                }
            }
        }

        // 对照 WPF: RefreshCreateCommitsCheckBoxVisibility
        private void RefreshCreateCommitsCheckBoxVisibility()
        {
            CreateCommitsCheckBox.IsVisible = _patchContainsCommitHeader;
        }

        // 对照 WPF: PatchContainsCommitHeader
        private static bool PatchContainsCommitHeader(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return false;
                return File.ReadAllText(filePath).StartsWith("From ");
            }
            catch (Exception ex)
            {
                Log.Error("Cannot apply patch", ex);
                return false;
            }
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            PathTextBox.IsEnabled = false;
            BrowseButton.IsEnabled = false;
            CreateCommitsCheckBox.IsEnabled = false;
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
