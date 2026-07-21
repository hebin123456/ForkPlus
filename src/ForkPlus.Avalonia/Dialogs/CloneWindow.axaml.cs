using System;
using System.IO;
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
    // Phase 4.x：Avalonia 版 CloneWindow（真实迁移版，对照 WPF CloneWindow.xaml.cs 550 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/CloneWindow.xaml.cs：
    //   - public partial class CloneWindow : ForkPlusDialogWindow
    //   - 嵌套 AccountItem / AccountItemType（Default/Account/Separator/Custom）
    //   - 字段: Account _account / bool _refreshingUrl / AccountItem[] AccountItems
    //   - 构造函数 (string url, Account account)
    //     * DialogTitle/Description = Translate("Clone") / Translate("Clone a remote repository into a local folder")
    //     * SubmitButtonTitle = Translate("Clone")
    //     * Refresh(url) → RepositoryUrlTextBox + ParentDirectoryTextBox（默认 RepositoryManager.Instance.DefaultSourceDir）
    //     * RefreshNetworkProtocolButton / UpdateSubmitButton / RefreshAccountsComboBox / HideStatusControls
    //   - OnSubmit:
    //     * 若 MainWindow.ActiveRepositoryUserControl != null: JobQueue.Add(RunClone foreground:false) + Close()
    //     * 否则: DisableEditableControls + SetStatus(InProgress) + Task.Run(RunClone foreground:true) + Close()
    //   - RunClone (foreground:progress + Application.TabManager().OpenRepository)
    //   - BrowseButton_Click: OpenDialog.SelectDirectory
    //   - RefreshRepositoryNameTextBox: 从 GitUrl.RepositoryName 派生
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. spike 省略 Test Connection / Network Protocol / SSH Key / Accounts ComboBox
    //      （需要额外基础设施，spike 版仅保留核心 URL + ParentDirectory + RepositoryName + Browse + CommandPreview）
    //   3. RepositoryManager.Instance.DefaultSourceDir() → 注入 Func<string>? defaultSourceDirProvider
    //   4. Application.Current.TabManager().OpenRepository(path) → 注入 Action<string>? onRepositoryOpened 回调
    //   5. RepositoryManager.Instance.SetSourceDirs(...) → 注入 Action<string>? onCloneCompleted 回调
    //   6. MainWindow.ActiveRepositoryUserControl.JobQueue.Add(...) → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   7. OpenDialog.SelectDirectory → StorageProvider.OpenFolderPickerAsync
    //   8. PlaceholderTextBox.Placeholder → TextBox Watermark
    //   9. spike 基类不提供 DisableEditableControls → 手动禁用 3 个 TextBox + Browse 按钮
    //  10. ServiceLocator.Clipboard.GetText() → spike 省略 TryParseUrlFromClipboard（依赖剪贴板服务）
    //  11. PreferencesLocalization → ServiceLocator.Localization.Translate
    public partial class CloneWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly Func<string>? _defaultSourceDirProvider;
        private readonly Action<string>? _onRepositoryOpened;
        private readonly Action<string>? _onCloneCompleted;
        private readonly string? _initialUrl;

        // 构造函数签名与 WPF 不同：注入 Func<string>? defaultSourceDirProvider + Action<string>? onRepositoryOpened
        // + Action<string>? onCloneCompleted 回调替代 RepositoryManager / MainWindow.TabManager 依赖
        public CloneWindow(
            string? url = null,
            Func<string>? defaultSourceDirProvider = null,
            Action<string>? onRepositoryOpened = null,
            Action<string>? onCloneCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _initialUrl = url;
            _defaultSourceDirProvider = defaultSourceDirProvider;
            _onRepositoryOpened = onRepositoryOpened;
            _onCloneCompleted = onCloneCompleted;

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle
            DialogTitle = Translate("Clone");
            DialogDescription = Translate("Clone a remote repository into a local folder");
            SubmitButtonTitle = Translate("Clone");
            CancelButtonTitle = Translate("Cancel");
            Title = Translate("Clone");

            // 对照 WPF: Refresh(url)
            Refresh(url);

            // 对照 WPF: HideStatusControls()（spike 省略状态控件，仅刷新 submit 状态）
            UpdateSubmitButton();
            RefreshCommandPreview();

            // 对照 WPF: Loaded += delegate { if (!string.IsNullOrEmpty(RepositoryUrlTextBox.Text.Trim())) { RepositoryNameTextBox.Focus(); SelectAll(); } }
            Loaded += (_, _) =>
            {
                if (!string.IsNullOrEmpty((RepositoryUrlTextBox.Text ?? "").Trim()))
                {
                    RepositoryNameTextBox.Focus();
                    RepositoryNameTextBox.SelectAll();
                }
            };
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed =>
            !string.IsNullOrWhiteSpace((RepositoryUrlTextBox.Text ?? "").Trim())
            && !string.IsNullOrWhiteSpace((RepositoryNameTextBox.Text ?? "").Trim())
            && !string.IsNullOrWhiteSpace((ParentDirectoryTextBox.Text ?? "").Trim());

        // 对照 WPF: protected override string GetCommandPreview()
        private string? GetCommandPreview()
        {
            string url = (RepositoryUrlTextBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;
            var parts = new System.Collections.Generic.List<string> { "git", "clone" };
            if (ForkPlusSettings.Default.UpdateSubmodulesOnCheckout)
            {
                parts.Add("--recurse-submodules");
            }
            parts.Add(Quote(url));
            string parentDir = (ParentDirectoryTextBox.Text ?? "").Trim();
            string repoName = (RepositoryNameTextBox.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(parentDir) && !string.IsNullOrWhiteSpace(repoName))
            {
                parts.Add(Quote(Path.Combine(parentDir, repoName)));
            }
            return string.Join(" ", parts);
        }

        private void RefreshCommandPreview()
        {
            if (CommandPreviewTextBox == null) return;
            CommandPreviewTextBox.Text = GetCommandPreview() ?? string.Empty;
        }

        // 对照 WPF: protected override async void OnSubmit()
        protected override async void OnSubmit()
        {
            try
            {
                string url = (RepositoryUrlTextBox.Text ?? "").Trim();
                string parentDir = (ParentDirectoryTextBox.Text ?? "").Trim();
                string repoName = (RepositoryNameTextBox.Text ?? "").Trim();
                string destinationDirectory = Path.Combine(parentDir, repoName);
                bool recurseSubmodules = ForkPlusSettings.Default.UpdateSubmodulesOnCheckout;

                DisableEditableControls();
                SetStatus(ForkPlusDialogStatus.InProgress, Translate("Cloning..."));

                // 对照 WPF: await Task.Run(delegate { RunClone(url, recurseSubmodules, destinationDirectory, new JobMonitor(), foreground: true); });
                GitCommandResult result = await Task.Run(delegate
                {
                    return RunClone(url, recurseSubmodules, destinationDirectory, new JobMonitor());
                });

                SetStatus(ForkPlusDialogStatus.None, string.Empty);
                EnableEditableControls();

                // 对照 WPF: if (!result.Succeeded && !monitor.IsCanceled) new ErrorWindow(...).ShowDialog();
                //                                          else Application.Current.TabManager().OpenRepository(destinationDirectory);
                // Avalonia spike: 仅在成功时回调 onRepositoryOpened + onCloneCompleted（错误处理交给调用方）
                if (result.Succeeded)
                {
                    try { _onCloneCompleted?.Invoke(destinationDirectory); }
                    catch (Exception ex) { Log.Error("CloneWindow onCloneCompleted callback failed", ex); }
                    try { _onRepositoryOpened?.Invoke(destinationDirectory); }
                    catch (Exception ex) { Log.Error("CloneWindow onRepositoryOpened callback failed", ex); }
                }
                else
                {
                    SetStatus(ForkPlusDialogStatus.Error, result.Error?.FriendlyDescription ?? Translate("Clone failed"));
                }
                Close();
            }
            catch (Exception ex)
            {
                Log.Error("CloneWindow OnSubmit failed", ex);
            }
        }

        // 对照 WPF: private void RunClone(string url, bool recurseSubmodules, string destinationDirectory, JobMonitor monitor, bool foreground)
        // Avalonia spike: 简化为单一前台模式，进度通过 SetStatus 推送
        private GitCommandResult RunClone(string url, bool recurseSubmodules, string destinationDirectory, JobMonitor monitor)
        {
            monitor.SetProgressAction(delegate
            {
                Dispatcher.UIThread.Post(delegate
                {
                    string message = monitor.ProgressMessage ?? "";
                    SetStatus(ForkPlusDialogStatus.InProgress, message ?? "");
                });
            });
            GitCommandResult result = new CloneGitCommand().Execute(url, recurseSubmodules, destinationDirectory, monitor);
            monitor.SetProgressAction(null);
            return result;
        }

        // 对照 WPF: private void Refresh(string url)
        private void Refresh(string? url)
        {
            if (url != null)
            {
                RepositoryUrlTextBox.Text = url;
            }
            // 对照 WPF: ParentDirectoryTextBox.Text = RepositoryManager.Instance.DefaultSourceDir();
            ParentDirectoryTextBox.Text = _defaultSourceDirProvider?.Invoke()
                ?? Environment.ExpandEnvironmentVariables("%userprofile%");
        }

        // 对照 WPF: RepositoryUrlTextBox_TextChanged
        public void RepositoryUrlTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshRepositoryNameTextBox();
            RefreshCommandPreview();
        }

        // 对照 WPF: ParentDirectoryTextBox_TextChanged
        public void ParentDirectoryTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: RepositoryNameTextBox_TextChanged
        public void RepositoryNameTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateSubmitButton();
            RefreshCommandPreview();
        }

        // 对照 WPF: BrowseButton_Click
        public async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            // 对照 WPF: string initialDirectory = RepositoryManager.Instance.DefaultSourceDir();
            string initialDirectory = _defaultSourceDirProvider?.Invoke()
                ?? Environment.ExpandEnvironmentVariables("%userprofile%");
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FolderPickerOpenOptions
            {
                Title = Translate("Select location"),
            };
            if (Directory.Exists(initialDirectory))
            {
                try
                {
                    var uri = new Uri(Path.GetFullPath(initialDirectory));
                    var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(uri);
                    if (folder != null) options.SuggestedStartLocation = folder;
                }
                catch { }
            }

            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (result != null && result.Count > 0)
            {
                ParentDirectoryTextBox.Text = result[0].Path.LocalPath;
                ParentDirectoryTextBox.Focus();
            }
        }

        // 对照 WPF: RefreshRepositoryNameTextBox
        private void RefreshRepositoryNameTextBox()
        {
            string? repositoryName = new GitUrl((RepositoryUrlTextBox.Text ?? "").Trim()).RepositoryName;
            if (repositoryName != null)
            {
                RepositoryNameTextBox.Text = repositoryName;
            }
        }

        // spike 版：手动禁用可编辑控件
        private void DisableEditableControls()
        {
            RepositoryUrlTextBox.IsEnabled = false;
            ParentDirectoryTextBox.IsEnabled = false;
            RepositoryNameTextBox.IsEnabled = false;
            BrowseButton.IsEnabled = false;
        }

        private void EnableEditableControls()
        {
            RepositoryUrlTextBox.IsEnabled = true;
            ParentDirectoryTextBox.IsEnabled = true;
            RepositoryNameTextBox.IsEnabled = true;
            BrowseButton.IsEnabled = true;
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
