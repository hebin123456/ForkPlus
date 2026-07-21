using System;
using System.Collections.Generic;
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
using ForkPlus.Undo;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.40b：Avalonia 版 ReflogWindow（真实迁移版，对照 WPF ReflogWindow.xaml.cs 203 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ReflogWindow.xaml.cs：
    //   - public partial class ReflogWindow : CustomWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl
    //   - 构造函数 (RepositoryUserControl repositoryUserControl)
    //   - LoadReflog: ReflogHistoryProvider.ReadHeadReflog(gitModule) + UndoIndexStore.Load() join
    //     → ReflogViewItem 列表 → ReflogListView.ItemsSource
    //   - JumpToSelected: MessageBoxWindow 确认 → _repositoryUserControl.AddUndoable
    //     → GitCommand("reset", "--hard", sha) + ExecuteWithCallbackBt → GitCommandResult
    //   - ReflogListView_MouseDoubleClick / JumpButton_Click → JumpToSelected
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. RepositoryUserControl 参数 → 注入 GitModule + Action<GitCommandResult>? onCompleted 回调
    //   3. spike 基类不提供 AddUndoable → 用 ResetCurrentBranchToRevisionGitCommand + Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   4. ListView+GridView → DataGrid（Avalonia 11 原生 DataGrid 更易表达列式布局）
    //   5. MouseDoubleClick → DoubleTapped 事件（Avalonia 同名）
    //   6. PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //   7. MessageBoxWindow 确认弹窗 → spike 版省略确认步骤（直接执行），onCompleted 回调通知调用方
    public partial class ReflogWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly GitModule _gitModule;
        private readonly Action<GitCommandResult>? _onCompleted;

        public ReflogWindow(GitModule gitModule, Action<GitCommandResult>? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _onCompleted = onCompleted;

            // 对照 WPF: ApplyLocalization
            string title = Translate("Reflog");
            Title = title;
            DialogTitle = title;
            HeaderTitle.Text = Translate("Reflog History");
            DialogDescription = Translate("Double-click an entry to jump to that state.");
            StatusText.Text = Translate("Double-click an entry to jump to that state.");
            RefreshButton.Content = Translate("Refresh");
            JumpButton.Content = Translate("Jump to...");
            CancelButtonTitle = Translate("Close");
            ShowSubmitButton = false;

            LoadReflog();
        }

        // 对照 WPF: private void LoadReflog()
        private void LoadReflog()
        {
            if (_gitModule == null)
            {
                StatusText.Text = Translate("No active repository.");
                ReflogItemsControl.ItemsSource = null;
                return;
            }

            List<ReflogEntry> reflog = new ReflogHistoryProvider().ReadHeadReflog(_gitModule);
            if (reflog.Count == 0)
            {
                StatusText.Text = Translate("Reflog is empty.");
                ReflogItemsControl.ItemsSource = null;
                return;
            }

            // 加载 UndoIndexStore 一次性 join
            Dictionary<string, UndoIndexEntry> index = new UndoIndexStore(_gitModule).Load();

            List<ReflogViewItem> items = new List<ReflogViewItem>(reflog.Count);
            foreach (ReflogEntry entry in reflog)
            {
                string operationName = entry.ReflogSubject ?? "";
                if (index.TryGetValue(entry.Sha, out UndoIndexEntry indexed) && !string.IsNullOrEmpty(indexed.OperationName))
                {
                    operationName = indexed.OperationName;
                }
                items.Add(new ReflogViewItem(entry, operationName));
            }
            ReflogItemsControl.ItemsSource = items;
            StatusText.Text = string.Format(FormatTranslate("{0} entries loaded."), items.Count);
        }

        // 对照 WPF: RefreshButton_Click
        public void RefreshButton_Click(object? sender, RoutedEventArgs e)
        {
            LoadReflog();
        }

        // 对照 WPF: ReflogListView_SelectionChanged
        public void ReflogItemsControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            JumpButton.IsEnabled = ReflogItemsControl.SelectedItem is ReflogViewItem;
        }

        // 对照 WPF: ReflogListView_MouseDoubleClick
        public void ReflogItemsControl_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            JumpToSelected();
        }

        // 对照 WPF: JumpButton_Click
        public void JumpButton_Click(object? sender, RoutedEventArgs e)
        {
            JumpToSelected();
        }

        // 对照 WPF: private void JumpToSelected()
        // spike 版：省略 MessageBoxWindow 确认弹窗（直接执行 reset --hard），onCompleted 回调通知调用方
        private void JumpToSelected()
        {
            if (!(ReflogItemsControl.SelectedItem is ReflogViewItem selected))
            {
                return;
            }
            if (string.IsNullOrEmpty(selected.Sha))
            {
                return;
            }
            Sha? sha = Sha.Parse(selected.Sha);
            if (!sha.HasValue)
            {
                return;
            }

            JumpButton.IsEnabled = false;
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Jumping to selected state..."));

            // 对照 WPF: _repositoryUserControl.AddUndoable(opName, delegate(JobMonitor monitor) { reset --hard <sha> })
            // Avalonia: Task.Run + Dispatcher.UIThread.Post + JobMonitor + ResetCurrentBranchToRevisionGitCommand
            Task.Run(delegate
            {
                JobMonitor monitor = new JobMonitor();
                GitCommandResult result = new ResetCurrentBranchToRevisionGitCommand().Execute(
                    _gitModule, sha.Value, BranchResetType.Hard, monitor);
                Dispatcher.UIThread.Post(delegate
                {
                    try
                    {
                        _onCompleted?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("ReflogWindow onCompleted callback failed", ex);
                    }
                    Close(result);
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

    // 对照 WPF: public sealed class ReflogViewItem
    // ReflogDataGrid 行视图模型（公开属性供 DataGrid 列绑定）
    public sealed class ReflogViewItem
    {
        private readonly ReflogEntry _entry;

        public ReflogViewItem(ReflogEntry entry, string operationName)
        {
            _entry = entry;
            OperationName = operationName ?? "";
        }

        // 完整 40 字符 sha。JumpToSelected 用它构造 reset --hard 参数。
        public string Sha => _entry.Sha ?? "";

        // reflog 索引（HEAD@{N} 的 N）。
        public int Index => _entry.Index;

        public string IndexDisplay => "HEAD@{" + _entry.Index + "}";

        public string ShaDisplay => string.IsNullOrEmpty(_entry.Sha) ? "" : _entry.Sha.Substring(0, Math.Min(8, _entry.Sha.Length));

        public string OperationName { get; }

        public string CommitSubject => _entry.CommitSubject ?? "";

        public string TimeDisplay => _entry.TimestampUtc.HasValue
            ? _entry.TimestampUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            : "";
    }
}
