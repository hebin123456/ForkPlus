using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.45b：Avalonia 版 GitLfsStatusWindow（真实迁移版，对照 WPF GitLfsStatusWindow.xaml.cs 265 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/GitLfsStatusWindow.xaml.cs：
    //   - public partial class GitLfsStatusWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / DelayedAction<bool> _refreshLfsFilesList /
    //           string[] _lfsFiles / Dictionary<string,string> _lfsLocks / LfsFileViewModel[] _selectedItems /
    //           Job _activeRefresLocksJob
    //   - 构造函数 (RepositoryUserControl) → JobQueue.Add 拉 GetLfsFilesGitCommand → Refresh 拉 GetLfsLocksGitCommand
    //   - ShowSubmitButton = false / CancelButtonTitle = "Close"
    //   - FilterTextBox (custom) + LfsFilesListBox + Lock/Unlock 按钮
    //   - LfsFilesListBox_SelectionChanged / LockButton_Click / UnlockButton_Click
    //   - Refresh: JobQueue.Add GetLfsLocksGitCommand → _refreshLfsFilesList.InvokeNow
    //   - RefreshLfsFilesList: 按 filter 过滤 + 排序 + 还原选中
    //   - CreateViewModels: 合并 lfsFiles + lfsLocks → LfsFileViewModel[]
    //   - Unlock/Lock: JobQueue.Add GitLfsUnlock/LockGitCommand → Refresh
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → GitModule + Action onCompleted 回调
    //   3. FilterTextBox (WPF 自定义控件) → 普通 TextBox + TextChanged + DelayedAction
    //   4. LfsFileViewModel (WPF, 依赖 ImageSource/INotifyPropertyChanged) → spike 内嵌简化版（只有 Path/Owner/DisplayText）
    //   5. ListBox 复杂 ItemTemplate (Image + AutoTooltipTextBlock + Lock icon) → spike 用 TextBlock 显示 path + owner
    //   6. FallbackUserControl → spike 用 SetStatus 表达 loading/error，不弹 ErrorWindow
    //   7. Keyboard.IsKeyDown (Ctrl+F) → spike 不接入快捷键（Avalonia KeyModifiers 检测可后续补）
    //   8. JobQueue + Dispatcher.Async → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   9. Job _activeRefresLocksJob + Monitor.Cancel → spike 用 _activeRefreshCancellationTokenSource
    //  10. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    public partial class GitLfsStatusWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Action<GitCommandResult>? _onCompleted;

        private readonly DelayedAction<bool> _refreshLfsFilesList;

        private string[] _lfsFiles = new string[0];
        private Dictionary<string, string> _lfsLocks = new Dictionary<string, string>();
        private LfsFileViewModel[] _selectedItems = new LfsFileViewModel[0];

        // 对照 WPF: Job _activeRefresLocksJob（用 CancellationTokenSource 替代 Job.Monitor.Cancel）
        private CancellationTokenSource? _activeRefreshCts;

        // 构造函数签名与 WPF 不同：用 GitModule + Action 回调替代 RepositoryUserControl
        // （RepositoryUserControl 在 Avalonia 端尚未迁移，spike 版解耦）
        public GitLfsStatusWindow(
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

            // 对照 WPF: DelayedAction<bool> _refreshLfsFilesList = new DelayedAction<bool>(RefreshLfsFilesList);
            _refreshLfsFilesList = new DelayedAction<bool>(RefreshLfsFilesList);

            // 对照 WPF: DialogTitle / DialogDescription / ShowSubmitButton / CancelButtonTitle
            string title = Translate("LFS Files Status");
            Title = title;
            DialogTitle = title;
            DialogDescription = "";
            ShowSubmitButton = false;
            CancelButtonTitle = Translate("Close");

            // 对照 WPF: SetStatus(InProgress, "Loading LFS files...")
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Loading LFS files..."));

            // 对照 WPF: repositoryUserControl.JobQueue.Add(Translate("LFS files"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult<string[]> lfsFilesResponse = new GetLfsFilesGitCommand().Execute(_gitModule, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    if (!lfsFilesResponse.Succeeded && !monitor.IsCanceled)
                    {
                        // spike: 用 SetStatus 表达错误，不弹 ErrorWindow（WPF 调 ErrorWindow）
                        SetStatus(ForkPlusDialogStatus.Error, lfsFilesResponse.Error?.FriendlyDescription ?? "Failed to load LFS files");
                    }
                    else
                    {
                        _lfsFiles = lfsFilesResponse.Result ?? Array.Empty<string>();
                        SetStatus(ForkPlusDialogStatus.None, "");
                        Refresh();
                    }
                });
            });
        }

        // 对照 WPF: FilterTextBox_FilterRequestChanged → _refreshLfsFilesList.InvokeWithDelay(true)
        public void FilterTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            _refreshLfsFilesList.InvokeWithDelay(true);
        }

        // 对照 WPF: LfsFilesListBox_SelectionChanged
        public void LfsFilesListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            _selectedItems = LfsFilesListBox.SelectedItems
                .OfType<LfsFileViewModel>()
                .ToArray();
        }

        // 对照 WPF: LockButton_Click
        public void LockButton_Click(object? sender, RoutedEventArgs e)
        {
            Lock(_selectedItems.Select(x => x.Path).ToArray());
        }

        // 对照 WPF: UnlockButton_Click
        public void UnlockButton_Click(object? sender, RoutedEventArgs e)
        {
            Unlock(_selectedItems.Select(x => x.Path).ToArray());
        }

        // 对照 WPF: private void Refresh()
        private void Refresh()
        {
            // 取消上一次未完成的 locks 刷新（对照 WPF: _activeRefresLocksJob?.Monitor.Cancel()）
            _activeRefreshCts?.Cancel();
            _activeRefreshCts = new CancellationTokenSource();
            CancellationToken token = _activeRefreshCts.Token;

            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Refreshing LFS locks..."));

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(Translate("LFS Locks"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult<Dictionary<string, string>> locksResponse = new GetLfsLocksGitCommand().Execute(_gitModule, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    _activeRefreshCts = null;
                    if (!locksResponse.Succeeded)
                    {
                        // spike: 用 SetStatus 表达错误，不弹 ErrorWindow
                        SetStatus(ForkPlusDialogStatus.Error, locksResponse.Error?.FriendlyDescription ?? "Failed to load LFS locks");
                    }
                    else
                    {
                        _lfsLocks = locksResponse.Result ?? new Dictionary<string, string>();
                        _refreshLfsFilesList.InvokeNow(true);
                        SetStatus(ForkPlusDialogStatus.None, "");
                    }
                });
            });
        }

        // 对照 WPF: private void RefreshLfsFilesList(bool dummy = false)
        private void RefreshLfsFilesList(bool dummy = false)
        {
            string? selectedPath = _selectedItems.FirstOrDefault()?.Path;
            LfsFileViewModel[] array = CreateViewModels(_lfsFiles, _lfsLocks);
            LfsFileViewModel[] filtered = array;
            string filterString = FilterTextBox.Text ?? "";
            if (!string.IsNullOrEmpty(filterString))
            {
                filtered = array
                    .Where(x => x.Path.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) != -1
                                || (x.Owner != null && x.Owner.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) != -1))
                    .ToArray();
            }
            LfsFilesListBox.ItemsSource = filtered;
            if (selectedPath != null)
            {
                LfsFilesListBox.SelectedItem = filtered.FirstOrDefault(x => x.Path == selectedPath);
            }
        }

        // 对照 WPF: private static LfsFileViewModel[] CreateViewModels(string[] lfsFiles, Dictionary<string,string> lfsLocks)
        private static LfsFileViewModel[] CreateViewModels(string[] lfsFiles, Dictionary<string, string> lfsLocks)
        {
            List<LfsFileViewModel> list = new List<LfsFileViewModel>(lfsFiles.Length);
            Dictionary<string, string> remaining = new Dictionary<string, string>(lfsLocks);
            foreach (string path in lfsFiles)
            {
                var vm = new LfsFileViewModel(path);
                if (remaining.TryGetValue(path, out var owner))
                {
                    vm.Owner = owner;
                    remaining.Remove(path);
                }
                list.Add(vm);
            }
            // 加入锁住但不在 lfsFiles 中的文件（对照 WPF: foreach dictionary2）
            foreach (KeyValuePair<string, string> item in remaining)
            {
                list.Add(new LfsFileViewModel(item.Key, item.Value));
            }
            list.Sort((x, y) => NaturalStringComparer.Instance.Compare(x.Path, y.Path));
            return list.ToArray();
        }

        // 对照 WPF: private void Unlock(string[] filepaths)
        private void Unlock(string[] filepaths)
        {
            if (filepaths.Length == 0)
            {
                return;
            }
            string message = (filepaths.Length == 1)
                ? string.Format(Translate("Unlocking '{0}'"), filepaths[0])
                : string.Format(Translate("Unlocking {0} files"), filepaths.Length);
            SetStatus(ForkPlusDialogStatus.InProgress, message);

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(Translate("LFS Unlock"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult unlockResult = new GitLfsUnlockGitCommand().Execute(_gitModule, filepaths, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    if (!unlockResult.Succeeded && !monitor.IsCanceled)
                    {
                        // spike: 用 SetStatus 表达错误，不弹 ErrorWindow
                        SetStatus(ForkPlusDialogStatus.Error, unlockResult.Error?.FriendlyDescription ?? "LFS unlock failed");
                    }
                    Refresh();
                });
            });
        }

        // 对照 WPF: private void Lock(string[] filepaths)
        private void Lock(string[] filepaths)
        {
            if (filepaths.Length == 0)
            {
                return;
            }
            string message = (filepaths.Length == 1)
                ? string.Format(Translate("Locking '{0}'"), filepaths[0])
                : string.Format(Translate("Locking {0} files"), filepaths.Length);
            SetStatus(ForkPlusDialogStatus.InProgress, message);

            // 对照 WPF: _repositoryUserControl.JobQueue.Add(Translate("LFS Lock"), ...)
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult lockResult = new GitLfsLockGitCommand().Execute(_gitModule, filepaths, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    if (!lockResult.Succeeded && !monitor.IsCanceled)
                    {
                        // spike: 用 SetStatus 表达错误，不弹 ErrorWindow
                        SetStatus(ForkPlusDialogStatus.Error, lockResult.Error?.FriendlyDescription ?? "LFS lock failed");
                    }
                    Refresh();
                });
            });
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

        // spike 内嵌版 LfsFileViewModel（对照 WPF src/ForkPlus/UI/Dialogs/LfsFileViewModel.cs 63 行）。
        // WPF 版继承 IRoundedSelectionListBoxViewModel + INotifyPropertyChanged，依赖 ImageSource (FileTypeIcon)。
        // spike 版简化：只保留 Path / Owner / DisplayText（绑定到 ListBox TextBlock）。
        private sealed class LfsFileViewModel
        {
            public string Path { get; }
            public string? Owner { get; set; }

            // 用于 ListBox ItemTemplate 的 TextBlock.Text 绑定
            // 对照 WPF ItemTemplate 用 Image + AutoTooltipTextBlock(Path) + Lock icon + Owner TextBlock
            // spike 版合并为 "path  (owner)" 单行显示
            public string DisplayText => string.IsNullOrEmpty(Owner) ? Path : Path + "  (" + Owner + ")";

            public LfsFileViewModel(string path, string? owner = null)
            {
                Path = path;
                Owner = owner;
            }
        }
    }
}
