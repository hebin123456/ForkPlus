using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 5.6a：Avalonia 版 InteractiveRebaseWindow（spike 骨架版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/InteractiveRebaseWindow.xaml.cs（919 行）：
    //   - public partial class InteractiveRebaseWindow : ForkPlusDialogWindow, IDisposable
    //   - 静态字段：InteractiveRebaseComboBoxItem[] InteractiveRebaseComboBoxItems（8 项）
    //   - 字段：GitModule _gitModule / LocalBranch _sourceBranch / IGitPoint _destination /
    //     IrAction _initialAction / IpcServer _riIpcServer / Task _riProcessRunner /
    //     Semaphore _finishRiProcessSemaphore / ObservableCollection<RevisionEntry> _todoList /
    //     DelayedAction<string> _refreshRevisionDetails /
    //     (InteractiveRebaseAction, string)[] _initialTodoList /
    //     bool _closing / _rebaseProcessRunning / _suppressRewordDialog / _updateInProgress /
    //     string _response / _todoListPath / CheckBox _backupCurrentStateCheckBox /
    //     RepositoryReferences _references / RewordAdorner _adorner
    //   - 构造函数 (RepositoryUserControl, GitModule, LocalBranch sourceBranch,
    //               IGitPoint destination, IrAction initialAction)：
    //     * UpdateRefsCheckBox.IsChecked = ForkPlusSettings.Default.InteractiveRebase_UpdateRefs
    //     * _backupCurrentStateCheckBox = new CheckBox（动态加到 Footer.CustomSection）
    //     * ShowCancelButton = true; SubmitButtonTitle = "Rebase"
    //     * SourceGitPointView.Value = _sourceBranch
    //     * DestinationGitPointView.Value = _destination
    //     * RevisionListView.ItemsSource = _todoList
    //     * _rebaseProcessRunning = true + _riProcessRunner = Task.Run 调
    //       RebaseInteractiveGitCommand().Execute(gitModule, destination)
    //   - GetCommandPreview: "git rebase -i <destination>"
    //   - OnClosing: 若 _rebaseProcessRunning 则 e.Cancel=true + StopRebaseInteractiveProcess("cancel")
    //   - OnSubmit:
    //     * SetStatus(InProgress, "Rebasing...")
    //     * contents = string.Concat(_todoList.Reverse().Select(x => x.AsTodoListString(updateRefs)))
    //     * File.WriteAllText(_todoListPath, contents)
    //     * SaveMessageArchiveForTodoList(_todoListPath)（JsonConvert.SerializeObject）
    //     * ForkPlusSettings.Default.InteractiveRebase_UpdateRefs = updateRefs
    //     * if (backup) CreateBackupBranch()
    //     * StopRebaseInteractiveProcess("start")
    //   - OnPreviewKeyDown: P/E/R/S/F/D 单键 + Ctrl+Up/Down（修改选中项 Action 或移动）
    //   - RevisionListView_MouseDoubleClick: ShowRewordPopup (Drop/Fixup/Squash 除外)
    //   - ComboBox_SelectionChanged: 选中 Move Up/Down 时调用 MoveUp/MoveDown，
    //     选中 Reword 时 ShowRewordPopup，其他设置 Action
    //   - PerformAction(IrAction): 根据 IrAction 类型修改 _todoList + MoveItemToRow
    //   - MoveUp / MoveDown / MoveItemToRow / GetRevisionBySha / GetRowBySha
    //   - ShowRewordPopup (RewordAdorner + RewordUserControl) + UpdateAdornerMargin
    //   - IpcMessageHandler: 解析 "prepareTodoListForRebase <path>" → PrepareTodoListForRebase
    //     + _finishRiProcessSemaphore.WaitOne(Timeout) + pipeServer.WriteString(_response)
    //   - PrepareTodoListForRebase: GetRebaseTodoListCommand().Execute(gitModule, todoListPath, _references)
    //     + _todoList.Clear + Populate RevisionEntry + PerformAction(_initialAction)
    //   - RevisionListViewItem_Drop: 拖拽重排 _todoList
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 构造函数重写：解耦 RepositoryUserControl，改为
    //      (GitModule, string upstreamRef, string? ontoRef, Action? onCompleted)
    //   2. spike 版用 ListBox + DataTemplate 显示 commit 列表（替代 WPF MultiselectionListView + GridView）
    //   3. 拖拽重排 spike 版省略（保留注释说明）— WPF 版 RevisionListViewItem_Drop + MoveUp/MoveDown
    //   4. IpcServer 通信 spike 版省略（保留注释说明）— WPF 版用 IpcServer 等 git rebase -i
    //      调起外部编辑器（RIHelperFilename）后回填 todo list，spike 版 OnSubmit 不通过 IpcServer
    //   5. spike 版省略 OnPreviewKeyDown 单键 + Ctrl+Up/Down
    //   6. spike 版省略 ShowRewordPopup（RewordAdorner + RewordUserControl，Adorner 不可用）
    //   7. spike 版省略 UpdateRefsCheckBox + _backupCurrentStateCheckBox
    //   8. spike 版省略 GetCommandPreview / RefreshCommandPreview
    //   9. spike 版省略 RevisionDetails / RevisionListFallbackUserControl
    //  10. spike 版省略 CreateBackupBranch + SaveMessageArchiveForTodoList
    //  11. spike 版省略 Dispose (IpcServer.Dispose)
    //  12. OnSubmit 直接调用 InteractiveRebaseGitCommand（若存在且 API 匹配）
    //      若不存在或 API 不匹配（实际只有 RebaseInteractiveGitCommand 且签名不匹配），
    //      spike 版直接调用 onCompleted?.Invoke() 并 Close
    //
    // 本 spike 版暂不迁移（留 Phase 5.6b 或更后）：
    //   - 自定义 MultiselectionListView + GridView
    //   - 拖拽重排（RevisionListViewItem_Drop + MoveUp/MoveDown + Adorner）
    //   - IpcServer 通信（_riIpcServer + _finishRiProcessSemaphore + IpcMessageHandler）
    //   - RewordAdorner + RewordUserControl 双击编辑 commit message
    //   - OnPreviewKeyDown 单键 P/E/R/S/F/D + Ctrl+Up/Down 快捷键
    //   - UpdateRefsCheckBox / _backupCurrentStateCheckBox
    //   - GetCommandPreview / RefreshCommandPreview
    //   - RevisionDetailsUserControl commit diff 详情
    //   - CreateBackupBranch + SaveMessageArchiveForTodoList
    //   - RebaseInteractiveGitCommand 真实调用（需 IpcServer 配合）
    //
    // 本 spike 版验证：
    //   - ListBox + DataTemplate + ComboBox 渲染 commit 列表正常
    //   - IrAction 枚举（Pick/Reword/Squash/Fixup/Edit/Drop/Skip）选择切换正常
    //   - 颜色标识 Border 按 Action 类型着色
    //   - OnSubmit 回调链路工作（onCompleted?.Invoke + Close）
    public partial class InteractiveRebaseWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // spike 版：内部定义 IrAction 枚举（WPF 在 ForkPlus.UI.Dialogs 命名空间，Core 不可访问）
        // 对照 WPF: enum IrAction { Squash, Fixup, Move, Drop, Reword, Edit }
        // spike 版扩展为完整 7 项（Pick/Reword/Squash/Fixup/Edit/Drop/Skip）以覆盖所有 ComboBox 选项
        public enum IrAction
        {
            Pick,
            Reword,
            Squash,
            Fixup,
            Edit,
            Drop,
            Skip
        }

        private readonly GitModule _gitModule;
        private readonly string _upstreamRef;
        private readonly string? _ontoRef;
        private readonly Action? _onCompleted;

        // spike 版：commit 列表数据源（ListBox 绑定）
        // 对照 WPF: ObservableCollection<RevisionEntry> _todoList
        private readonly ObservableCollection<InteractiveRebaseItemViewModel> _todoList
            = new ObservableCollection<InteractiveRebaseItemViewModel>();

        // 构造函数（spike 版签名）：
        // 对照 WPF: (RepositoryUserControl, GitModule, LocalBranch sourceBranch,
        //            IGitPoint destination, IrAction initialAction)
        // Avalonia: (GitModule, string upstreamRef, string? ontoRef, Action? onCompleted)
        public InteractiveRebaseWindow(
            GitModule gitModule,
            string upstreamRef,
            string? ontoRef = null,
            Action? onCompleted = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _gitModule = gitModule ?? throw new ArgumentNullException(nameof(gitModule));
            _upstreamRef = upstreamRef ?? throw new ArgumentNullException(nameof(upstreamRef));
            _ontoRef = ontoRef;
            _onCompleted = onCompleted;

            // 对照 WPF: DialogTitle / DialogDescription
            DialogTitle = Translate("Interactive Rebase");
            DialogDescription = FormatCurrent("Rebase commits from {0} onto {1}",
                _upstreamRef, _ontoRef ?? _upstreamRef);
            CancelButtonTitle = Translate("Cancel");
            SubmitButtonTitle = Translate("Rebase");
            Title = Translate("Interactive Rebase");

            // 对照 WPF: SourceGitPointView.Value = _sourceBranch / DestinationGitPointView.Value = _destination
            UpstreamRefTextBlock.Text = _upstreamRef;
            OntoRefTextBlock.Text = _ontoRef ?? Translate("(same as upstream)");

            // 绑定 ListBox
            CommitListBox.ItemsSource = _todoList;

            // 对照 WPF: RevisionListFallbackUserControl.Show() + FallbackMessage = "Loading..."
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Loading..."));

            // 后台加载 commit 列表（spike 版用 git log 直接拉取，不通过 IpcServer）
            LoadCommitListAsync();
        }

        // spike 版：后台加载 commit 列表
        // 对照 WPF: PrepareTodoListForRebase (GetRebaseTodoListCommand + IpcServer)
        // Avalonia: 用 git log upstreamRef..HEAD 直接拉取（不通过 IpcServer + RebaseInteractiveGitCommand）
        private void LoadCommitListAsync()
        {
            Task.Run(() =>
            {
                try
                {
                    // spike 版：用 git log 拉取 upstreamRef..HEAD 之间的 commit
                    // 对照 WPF: GetRebaseTodoListCommand 解析 todo file
                    string range = string.IsNullOrEmpty(_ontoRef)
                        ? _upstreamRef + "..HEAD"
                        : _upstreamRef + "..HEAD";

                    // %H = full sha, %x1f = ASCII 0x1f (unit separator) as field delimiter,
                    // %s = subject
                    GitRequestResult result = new GitRequest(_gitModule)
                        .Command("log", "--no-show-signature", "--pretty=format:%H%x1f%s", "--reverse", range)
                        .Execute(silent: true);

                    if (!result.Success)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            SetStatus(ForkPlusDialogStatus.Error,
                                result.Stderr ?? Translate("Failed to load commit list"));
                        });
                        return;
                    }

                    var items = new List<InteractiveRebaseItemViewModel>();
                    string[] lines = (result.Stdout ?? string.Empty).Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        int sep = line.IndexOf('\x1f');
                        string sha = sep >= 0 ? line.Substring(0, sep) : line;
                        string message = sep >= 0 && sep + 1 < line.Length ? line.Substring(sep + 1) : "";
                        items.Add(new InteractiveRebaseItemViewModel
                        {
                            Sha = sha,
                            Message = message,
                            Action = IrAction.Pick,
                            IsEditable = true
                        });
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        _todoList.Clear();
                        foreach (var item in items)
                        {
                            _todoList.Add(item);
                        }
                        ClearStatus();
                        UpdateSubmitButton();
                    });
                }
                catch (Exception ex)
                {
                    Log.Error("InteractiveRebaseWindow LoadCommitListAsync failed", ex);
                    Dispatcher.UIThread.Post(() =>
                    {
                        SetStatus(ForkPlusDialogStatus.Error, Translate("Failed to load commit list"));
                    });
                }
            });
        }

        // 对照 WPF: protected override bool IsSubmitAllowed
        protected override bool IsSubmitAllowed
        {
            get
            {
                if (_todoList.Count == 0)
                {
                    return false;
                }
                return !IsOperationInProgress;
            }
        }

        // 对照 WPF: protected override void OnSubmit()
        // spike 版：构造 rebase todo 文件路径 + 尝试调用 InteractiveRebaseGitCommand
        //          若 InteractiveRebaseGitCommand 不存在或 API 不匹配（实际只有 RebaseInteractiveGitCommand
        //          且签名 Execute(GitModule, IGitPoint) 不匹配 spike 的 (gitModule, upstreamRef, ontoRef, todoFile, monitor)）
        //          则 spike 版直接调用 onCompleted?.Invoke() 并 Close
        protected override void OnSubmit()
        {
            SetStatus(ForkPlusDialogStatus.InProgress, Translate("Rebasing..."));

            // spike 版：构造 rebase todo 文件路径（仅用于演示，实际不写入）
            // 对照 WPF: _todoListPath (由 IpcServer 传入)
            string todoListPath = Path.Combine(Path.GetTempPath(),
                "forkplus-ir-todo-" + Guid.NewGuid().ToString("N"));

            // spike 版：构造 rebase 命令序列（仅用于演示）
            // 对照 WPF: contents = string.Concat(_todoList.Reverse().Select(x => x.AsTodoListString(updateRefs)))
            //          File.WriteAllText(_todoListPath, contents)

            // spike 版：尝试调用 InteractiveRebaseGitCommand（不存在）→ fallback 到 onCompleted?.Invoke + Close
            // 对照 WPF: RebaseInteractiveGitCommand().Execute(gitModule, destination) + IpcServer 通信
            // spike 版 fallback：
            try
            {
                // 试图调用 InteractiveRebaseGitCommand（不存在；实际类是 RebaseInteractiveGitCommand
                // 且签名不匹配 spike 的 (gitModule, upstreamRef, ontoRef, todoFile, monitor)）
                // → 走 fallback：直接调用 onCompleted?.Invoke() 并 Close
                // 注意：spike 版不实际执行 git rebase -i（依赖 IpcServer + RebaseInteractiveGitCommand
                //       的真实链路在 Phase 5.6b 接入）

                ClearStatus();
                InvokeOnCompleted();
                Close();
            }
            catch (Exception ex)
            {
                Log.Error("InteractiveRebaseWindow OnSubmit failed", ex);
                SetStatus(ForkPlusDialogStatus.Error, ex.Message);
            }
        }

        // spike 版：调用 onCompleted 回调
        // 对照 WPF: Close(GitCommandResult) 退出协议
        private void InvokeOnCompleted()
        {
            try
            {
                _onCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("InteractiveRebaseWindow onCompleted callback failed", ex);
            }
        }

        // 对照 WPF: private static string Translate(string text)
        // spike 版：用 ServiceLocator.Localization.Translate 替代 PreferencesLocalization.Translate
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

        // 对照 WPF: PreferencesLocalization.FormatCurrent
        // spike 版：用 ServiceLocator.Localization.FormatCurrent 替代
        private static string FormatCurrent(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            if (localization != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }

    // spike 版：commit 列表项 ViewModel（供 ListBox + DataTemplate 绑定）
    // 对照 WPF: RevisionEntry (ObservableCollection 元素)
    //          * Row / Sha / Action / CustomMessage / GroupMessage / GraphType / OriginalMessage 等
    // spike 版简化为：Sha / Message / Action / IsEditable + ActionBrush（颜色标识）
    public class InteractiveRebaseItemViewModel
    {
        // commit sha（完整 40 字符）
        public string Sha { get; set; } = string.Empty;

        // commit subject（首行 message）
        public string Message { get; set; } = string.Empty;

        // 用户选择的 rebase action（Pick/Reword/Squash/Fixup/Edit/Drop/Skip）
        public InteractiveRebaseWindow.IrAction Action { get; set; } = InteractiveRebaseWindow.IrAction.Pick;

        // spike 版：是否可编辑（Reword action 触发弹窗时用，spike 版省略弹窗仍保留属性）
        public bool IsEditable { get; set; } = true;

        // ComboBox 数据源（所有可选 action）
        public IEnumerable<InteractiveRebaseWindow.IrAction> AvailableActions
            => Enum.GetValues(typeof(InteractiveRebaseWindow.IrAction))
                   .Cast<InteractiveRebaseWindow.IrAction>();

        // spike 版：sha 短格式（前 7 字符）
        public string ShaShort => string.IsNullOrEmpty(Sha) ? "" : Sha.Length > 7 ? Sha.Substring(0, 7) : Sha;

        // spike 版：按 Action 类型着色（左侧颜色标识 Border 的 Background）
        // 对照 WPF: GraphType 颜色映射
        public IBrush ActionBrush
        {
            get
            {
                return Action switch
                {
                    InteractiveRebaseWindow.IrAction.Pick => Brushes.Green,
                    InteractiveRebaseWindow.IrAction.Reword => Brushes.DodgerBlue,
                    InteractiveRebaseWindow.IrAction.Squash => Brushes.MediumPurple,
                    InteractiveRebaseWindow.IrAction.Fixup => Brushes.MediumSlateBlue,
                    InteractiveRebaseWindow.IrAction.Edit => Brushes.Orange,
                    InteractiveRebaseWindow.IrAction.Drop => Brushes.Crimson,
                    InteractiveRebaseWindow.IrAction.Skip => Brushes.Gray,
                    _ => Brushes.Gray,
                };
            }
        }
    }
}
