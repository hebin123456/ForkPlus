using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Interaction;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.x：Avalonia 版 FileHistoryWindow（spike 简化版，对照 WPF FileHistoryWindow.xaml.cs 661 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/FileHistoryWindow.xaml.cs：
    //   - public partial class FileHistoryWindow : CustomWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / Sha? _revisionToSelect
    //           Reference _targetReference / Mode _mode / DelayedAction<HistoryEntryViewModel[]> _delayedAction
    //           RevisionWithFiles[] _revisions / string[] _patches
    //           HistoryEntryViewModel[] _selectedHistoryEntries / MultiselectionTreeViewItem _root
    //           Job _showDiffJob
    //   - 构造函数 (RepositoryUserControl repositoryUserControl, Mode mode, Sha? revisionToSelect, Reference targetReference)
    //     * TreeView.SelectionMode = Single / Extended
    //     * FileDiffControl.Target = HunkHistory / History
    //     * NotificationCenter.DiffContextSizeChanged / DiffIgnoreWhitespacesChanged / DiffShowEntireFileChanged
    //       → _delayedAction.ReinvokeNow()
    //     * CommandBindings: OpenFileInDefaultEditor / CopyRevisionSha / CopyRevisionInfo / RunExternalDiffTool
    //   - OnInitialized: 异步调 GetFileHistoryGitCommand → _revisions / TreeView.RootItem = _root
    //     + HistoryEntryViewModel / FolderHistoryEntryViewModel / SubItemFileHistoryEntryViewModel
    //     + 按 _revisionToSelect 选中
    //   - TreeView_SelectionChanged: _selectedHistoryEntries + _delayedAction.InvokeWithDelay
    //   - TreeView_ContextMenuOpening: ShowRevisionInSeparateWindow / Reveal in Fork / OpenFileInDefaultEditor /
    //     ShowFileInFileExplorer / ResetFileToStateAtRevision / SaveFile / CustomCommand / External Diff
    //   - TreeView_MouseDoubleClick: RevealRevision(historyEntryViewModel.Sha)
    //   - RefreshDiff(HistoryEntryViewModel[] historyEntries):
    //     * historyEntries.Length > 2 → FallbackUserControl "Select two commits"
    //     * Hunk mode → ParsePatch(diffString, changedFile, gitModule, tabWidth)
    //     * File mode → GetRevisionFileChangesGitCommand → FileDiffControl.Content
    //   - OnKeyDown: Escape → Close
    //   - OnSourceInitialized / OnLocationChanged / Window_SizeChanged → ForkPlusSettings.HistoryWindowLocationState
    //
    // Avalonia 版 spike 简化项（与 WPF 行为差异）：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. 构造函数签名：(GitModule gitModule, string filePath, Action<string>? onCommitSelected = null)
    //      替代 (RepositoryUserControl, Mode, Sha?, Reference)
    //   3. MultiselectionTreeView + HistoryEntryViewModel / FolderHistoryEntryViewModel /
    //      SubItemFileHistoryEntryViewModel + RevisionTimeLine + FallbackUserControl →
    //      spike 版用 ListBox 显示 HistoryEntryViewModel 列表（spike 版内联 POCO，无 Folder/Hunk 模式）
    //   4. FileDiffControl（WPF 自定义 UserControl，承载 diff 渲染）→ AvaloniaEdit.TextEditor（只读）
    //      spike 版不渲染 diff，仅显示选中 commit 的该文件内容（git show <sha>:<path>）
    //   5. GetFileHistoryGitCommand → spike 版直接调
    //      `git log --follow --format="%H|%an|%ae|%ad|%s" --date=iso -- <path>` 一次性获取
    //   6. 选中 commit 时后台执行 `git show <sha>:<path>` 获取该版本文件内容并显示
    //   7. TreeView_SelectionChanged 多选 → spike 版仅支持单选
    //   8. CommandBindings 快捷键 → spike 版省略
    //   9. ContextMenu 菜单项 → spike 版省略
    //  10. ForkPlusSettings.HistoryWindowLocationState 持久化 → spike 版跳过
    //  11. TreeView_MouseDoubleClick → onCommitSelected 回调通知调用方
    //  12. Dispatcher.Async → Dispatcher.UIThread.Post
    //  13. await Task.Run(...) → Task.Run + Dispatcher.UIThread.Post
    //  14. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //
    // 内联 POCO 类:
    //   - HistoryEntryViewModel: Sha / Author / AuthorEmail / AuthorDate / Subject
    //     + 派生属性 ShaDisplay / AuthorDateDisplay（用于 DataTemplate 绑定）
    public partial class FileHistoryWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // spike 版：内联简化 HistoryEntryViewModel POCO
        // 对照 WPF: HistoryEntryViewModel (基于 RevisionWithFiles + ChangedFile)
        // 字段: Sha / Author / AuthorEmail / AuthorDate / Subject
        public sealed class HistoryEntryViewModel
        {
            public string Sha { get; set; }
            public string Author { get; set; }
            public string AuthorEmail { get; set; }
            public DateTime AuthorDate { get; set; }
            public string Subject { get; set; }

            // 派生属性用于 DataTemplate 绑定
            public string ShaDisplay => string.IsNullOrEmpty(Sha) ? "" : (Sha.Length >= 8 ? Sha.Substring(0, 8) : Sha);
            public string AuthorDateDisplay => AuthorDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private readonly GitModule _gitModule;
        private readonly string _filePath;
        private readonly Action<string> _onCommitSelected;

        private readonly ObservableCollection<HistoryEntryViewModel> _history = new ObservableCollection<HistoryEntryViewModel>();
        // 防重入：避免 SelectionChanged 在程序设置 SelectedIndex 时递归触发
        private bool _suppressSelectionChanged;
        // 当前正在加载的 sha（防止旧任务覆盖新结果）
        private string _loadingSha;

        public FileHistoryWindow(GitModule gitModule, string filePath, Action<string>? onCommitSelected = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _onCommitSelected = onCommitSelected;

            // 对照 WPF: base.Title = PathHelper.GetReadableFileName(mode.Path) + " - " + Translate("History")
            string title = PathHelper.GetReadableFileName(filePath) + " - " + Translate("History");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Show commit history of the file.");
            CancelButtonTitle = Translate("Close");
            ShowSubmitButton = false;

            // 对照 WPF: FileNameTextBlock.FilePath = mode.Path;
            FilePathTextBlock.Text = filePath;

            // 绑定 ListBox 数据源
            HistoryListBox.ItemsSource = _history;

            // 后台启动 git log --follow 解析
            // 对照 WPF: OnInitialized → await Task.Run(() => new GetFileHistoryGitCommand().Execute(...))
            // Avalonia: Task.Run + Dispatcher.UIThread.Post
            LoadHistoryAsync();
        }

        // 对照 WPF: OnInitialized → GetFileHistoryGitCommand.Execute
        // spike 版：直接调 `git log --follow --format="%H|%an|%ae|%ad|%s" --date=iso -- <path>`
        private void LoadHistoryAsync()
        {
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Loading history..."));
            Task.Run(delegate
            {
                try
                {
                    var gitCommand = new GitCommand(
                        "log",
                        "--follow",
                        "--format=%H|%an|%ae|%ad|%s",
                        "--date=iso",
                        "--",
                        _filePath);
                    var result = new GitRequest(_gitModule).Command(gitCommand).Execute(silent: true);
                    if (!result.Success)
                    {
                        Dispatcher.UIThread.Post(delegate
                        {
                            ShowErrorFallback(result.Stderr.Length > 0 ? result.Stderr : result.Stdout);
                        });
                        return;
                    }
                    var entries = ParseHistoryLog(result.Stdout);
                    Dispatcher.UIThread.Post(delegate
                    {
                        ApplyHistory(entries);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        ShowErrorFallback(ex.Message);
                    });
                }
            });
        }

        // spike 版：内联简化 git log --format 解析器
        // 每行格式: <sha>|<author-name>|<author-email>|<author-date>|<subject>
        private List<HistoryEntryViewModel> ParseHistoryLog(string output)
        {
            var result = new List<HistoryEntryViewModel>();
            if (string.IsNullOrEmpty(output)) return result;

            string[] lines = output.Split('\n');
            foreach (var raw in lines)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string line = raw.TrimEnd('\r');
                var parts = line.Split('|');
                if (parts.Length < 5) continue;
                string sha = parts[0];
                string author = parts[1];
                string email = parts[2];
                string dateStr = parts[3];
                // subject 可能包含 | 字符（commit message 中），用 string.Join 还原
                string subject = string.Join("|", parts, 4, parts.Length - 4);
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var authorDate))
                {
                    // git iso 格式: 2024-01-15 14:30:00 +0800
                    if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss zzz",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out authorDate))
                    {
                        authorDate = DateTime.MinValue;
                    }
                }
                result.Add(new HistoryEntryViewModel
                {
                    Sha = sha,
                    Author = author,
                    AuthorEmail = email,
                    AuthorDate = authorDate,
                    Subject = subject
                });
            }
            return result;
        }

        // 对照 WPF: TreeView.RootItem = _root + TreeView.SelectedItem = historyEntryViewModel
        // spike 版：清空 _history 后填充 + 默认选中第一项
        private void ApplyHistory(List<HistoryEntryViewModel> entries)
        {
            _suppressSelectionChanged = true;
            _history.Clear();
            foreach (var entry in entries)
            {
                _history.Add(entry);
            }
            _suppressSelectionChanged = false;

            SetStatus(ForkPlusDialogStatus.None, "");
            StatusText.Text = FormatTranslate("{0} commits", _history.Count);

            // 默认选中第一项触发文件内容加载
            if (_history.Count > 0)
            {
                HistoryListBox.SelectedIndex = 0;
            }
        }

        // 对照 WPF: TreeView_SelectionChanged → _delayedAction.InvokeWithDelay(_selectedHistoryEntries)
        // spike 版：单选模式，选中项变化时后台加载该版本文件内容
        public void HistoryListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;
            if (HistoryListBox.SelectedItem is HistoryEntryViewModel entry)
            {
                LoadFileContentAsync(entry.Sha);
            }
        }

        // 对照 WPF: TreeView_MouseDoubleClick → RevealRevision(historyEntryViewModel.Sha)
        // spike 版：双击触发 onCommitSelected 回调通知调用方
        public void HistoryListBox_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (HistoryListBox.SelectedItem is HistoryEntryViewModel entry)
            {
                try
                {
                    _onCommitSelected?.Invoke(entry.Sha);
                }
                catch (Exception ex)
                {
                    Log.Error("FileHistoryWindow onCommitSelected callback failed", ex);
                }
            }
        }

        // 对照 WPF: RefreshDiff → GetRevisionFileChangesGitCommand → FileDiffControl.Content
        // spike 版：直接调 `git show <sha>:<path>` 获取该版本文件内容并显示到 AvaloniaEdit
        private void LoadFileContentAsync(string sha)
        {
            if (string.IsNullOrEmpty(sha)) return;
            _loadingSha = sha;
            SetStatus(ForkPlusDialogStatus.InProgress, FormatTranslate("Loading content of {0}...", sha.Length >= 8 ? sha.Substring(0, 8) : sha));
            var capturedSha = sha;
            Task.Run(delegate
            {
                try
                {
                    var gitCommand = new GitCommand("show", $"{capturedSha}:{_filePath}");
                    var result = new GitRequest(_gitModule).Command(gitCommand).Execute(silent: true);
                    Dispatcher.UIThread.Post(delegate
                    {
                        // 防止旧任务覆盖新结果
                        if (_loadingSha != capturedSha) return;
                        if (!result.Success)
                        {
                            ShowErrorFallback(result.Stderr.Length > 0 ? result.Stderr : result.Stdout);
                            return;
                        }
                        FileContentEditor.Document.Text = result.Stdout ?? "";
                        SetStatus(ForkPlusDialogStatus.None, "");
                        StatusText.Text = FormatTranslate("{0} commits", _history.Count);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(delegate
                    {
                        if (_loadingSha != capturedSha) return;
                        ShowErrorFallback(ex.Message);
                    });
                }
            });
        }

        // 对照 WPF: ShowErrorFallback
        private void ShowErrorFallback(string errorString)
        {
            SetStatus(ForkPlusDialogStatus.Error, errorString);
            StatusText.Text = Translate("Error") + ": " + errorString;
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
