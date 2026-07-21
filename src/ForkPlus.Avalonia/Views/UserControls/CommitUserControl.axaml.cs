using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 5.3 完整迁移版：Avalonia 版 CommitUserControl。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/CommitUserControl.xaml.cs（2339 行）：
    //   - 18 个公共方法/属性（Initialize / ApplyLocalization / Refresh / StageSelectedFiles /
    //     LoadCommitMessage / SaveCommitMessage / EraseSavedCommitMessage / UpdateCommitMode /
    //     RefreshStageControls / UpdateCommitSection / FocusCommitMessageField / ToggleShowIgnoredFiles
    //     + 属性 RepositoryUserControl / FullCommitMessage / AmendMode / IsCommitAllowed /
    //     CommittingInProgress / StageJob / ShowIgnoredFiles / DontRefreshOnAmend）
    //   - Commit 流程：JobQueue.Add("Commit", monitor => new CommitGitCommand().Execute(...))
    //   - StageAllFiles / UnstageAllFiles / StageSelectedFiles / UnstageSelectedFiles /
    //     DiscardSelectedFiles（通过 Commands 容器 + JobQueue 调 git）
    //   - UpdateDiff（DelayedAction<ChangedFileArgs>）+ LoadWorkingDirectoryDiff git diff 调用
    //   - SubjectLengthLimit（72/50 规则）+ CommitButtonTitle（按 staged 计数 + amend 模式）
    //   - NotificationCenter WeakEventManager 事件订阅
    //   - DiffPopupWindow 弹窗 / AI commit 生成 / Gitmoji 自动补全
    //
    // Avalonia 版差异（Phase 5.3 完整迁移）：
    //   - JobQueue → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   - PreferencesLocalization → ServiceLocator.Localization
    //   - CommitMessageAutocompleteProvider / GitmojiAutocompleteProvider → 省略自动补全（保留注释）
    //   - 拼写检查 → 省略（Avalonia 不原生支持）
    //   - StageFileUserControl 双列表 → 内联 ObservableCollection<FileRow> + DataTemplate
    //   - CommitSubjectTextBox + CommitDescriptionTextBox → 单个 AvaloniaEdit.TextEditor
    //     （CommitMessageTextBox，Document.Text 替代 .Text）
    //   - DropDownButton（最近消息/Commit 设置下拉）→ 普通 Button
    //   - DiffPopupWindow 弹窗 → 省略
    //   - NotificationCenter 事件订阅 → 省略（不可访问）
    //   - Application.Current.Resources[key] → TryGetResource(key, null, out var v)
    //   - Visibility.Collapsed/Visible → IsVisible = false/true
    //   - Dispatcher.Invoke → Dispatcher.UIThread.Post
    //   - MainWindow.Instance 依赖 → 注入回调 OnRepositoryRefresh / OnOpenRepository
    //   - 状态图标 PNG → emoji（M=📝 / A=✨ / D=🗑 / R=🔀 / Untracked=❓）
    //
    // task spec 简化 API（本版本实现）：
    //   SetCommitMessage / GetCommitMessage / StageFile / UnstageFile / StageAll / UnstageAll /
    //   DiscardChanges / Commit / CommitAndPush / AmendCommit
    //   属性：CommitMessage / StagedFiles / UnstagedFiles / SelectedFile
    //
    // 构造函数：
    //   - CommitUserControl()：无参，供 XAML 实例化（RepositoryContentUserControl.axaml）
    //   - CommitUserControl(IServiceProvider)：供 DI 解析（ServiceCollectionExtensions 注册）
    //   两个构造函数都调用 InitializeComponent() + 共享初始化
    public partial class CommitUserControl : UserControl
    {
        // ===== 内部类：文件行模型（供 ItemsControl DataTemplate 绑定）=====
        // 对照 WPF FileListUserControl 内的 ChangedFile 项 + ChangeTypeIcon
        // 实现 INotifyPropertyChanged 以支持 OneWay 绑定（IsStaged 变化时更新 CheckBox）
        private class FileRow : INotifyPropertyChanged
        {
            private bool _isStaged;
            public bool IsStaged
            {
                get => _isStaged;
                set
                {
                    if (_isStaged != value)
                    {
                        _isStaged = value;
                        OnPropertyChanged();
                    }
                }
            }
            public string FilePath { get; set; }
            public string StatusEmoji { get; set; }
            public ChangeType ChangeType { get; set; }
            public ChangedFile File { get; set; }
            // DataTemplate 绑定 Path（避免与 System.IO.Path 混淆，命名为 FilePath 但绑定时用 Path 属性）
            // 注意：XAML 绑定 {Binding Path} 会绑定到名为 "Path" 的成员，故此处提供 Path 别名
            public string Path => FilePath;

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ===== 私有字段（对照 WPF）=====
        private readonly IServiceProvider _serviceProvider;
        private readonly ObservableCollection<FileRow> _fileRows = new ObservableCollection<FileRow>();
        private ChangedFile[] _allChangedFiles = Array.Empty<ChangedFile>();
        private FileRow _selectedRow;
        private bool _isLoaded;
        private bool _refreshing;
        private bool _suppressTextChanged;
        private int _stagedDiffStatsRequestId;

        // 对照 WPF _rebaseAmendSha / _pendingRepositoryStatusUiRefresh / _diffPopupWindow
        private string _rebaseAmendSha;
        private bool _pendingRepositoryStatusUiRefresh;

        // task spec: 注入回调替代 MainWindow.Instance 依赖
        public Action OnRepositoryRefresh { get; set; }
        public Action<string> OnOpenRepository { get; set; }

        // ===== 公共属性（对照 WPF 8 个公共属性 + task spec 4 个属性）=====

        // 对照 WPF: public RepositoryUserControl RepositoryUserControl（spike 用 object）
        public object RepositoryUserControl { get; private set; }

        // 对照 WPF: public GitModule GitModule（spike 新增，供 git 命令调用；
        //   WPF 通过 RepositoryUserControl.GitModule 访问，spike 的 RepositoryUserControl 是 object，
        //   故提供独立属性，由 Initialize 或外部调用方设置）
        public GitModule GitModule { get; set; }

        // 对照 WPF: public string FullCommitMessage
        //   WPF 版从 CommitSubjectTextBox.Text + CommitDescriptionTextBox.Text 拼接
        //   spike 版从 AvaloniaEdit CommitMessageTextBox.Document.Text 读取
        public string FullCommitMessage
        {
            get
            {
                if (CommitMessageTextBox?.Document != null)
                {
                    return CommitMessageTextBox.Document.Text;
                }
                return string.Empty;
            }
            set
            {
                if (CommitMessageTextBox?.Document != null)
                {
                    _suppressTextChanged = true;
                    CommitMessageTextBox.Document.Text = value ?? string.Empty;
                    _suppressTextChanged = false;
                }
            }
        }

        // task spec: CommitMessage（FullCommitMessage 的别名）
        public string CommitMessage
        {
            get => FullCommitMessage;
            set => FullCommitMessage = value;
        }

        // 对照 WPF: public bool AmendMode（映射 AmendCheckBox.IsChecked）
        public bool AmendMode
        {
            get => AmendCheckBox?.IsChecked ?? false;
            set
            {
                if (AmendCheckBox != null)
                {
                    AmendCheckBox.IsChecked = value;
                }
            }
        }

        // 对照 WPF: public bool CommittingInProgress
        public bool CommittingInProgress { get; set; }

        // 对照 WPF: public Job StageJob（spike 用 object 占位）
        public object StageJob { get; set; }

        // 对照 WPF: public bool ShowIgnoredFiles
        public bool ShowIgnoredFiles { get; set; }

        // 对照 WPF: public bool DontRefreshOnAmend
        public bool DontRefreshOnAmend { get; set; }

        // 对照 WPF: public bool IsCommitAllowed
        //   判断是否允许提交：无 StageJob / 无 CommittingInProgress / 有 staged 文件 / 有 commit 消息
        public bool IsCommitAllowed
        {
            get
            {
                if (StageJob != null) return false;
                if (CommittingInProgress) return false;
                int stagedCount = StagedFilesCount;
                if (stagedCount == 0 && !AmendMode) return false;
                // 对照 WPF: gitModule.Settings.SkipCommitMessage
                if (GitModule != null && GitModule.Settings != null && GitModule.Settings.SkipCommitMessage)
                {
                    return true;
                }
                if (string.IsNullOrWhiteSpace(FullCommitMessage)) return false;
                return true;
            }
        }

        // task spec: StagedFiles / UnstagedFiles / SelectedFile
        public ChangedFile[] StagedFiles => _fileRows.Where(r => r.IsStaged).Select(r => r.File).Where(f => f != null).ToArray();
        public ChangedFile[] UnstagedFiles => _fileRows.Where(r => !r.IsStaged).Select(r => r.File).Where(f => f != null).ToArray();
        public ChangedFile SelectedFile => _selectedRow?.File;

        // staged 文件计数（内部用）
        private int StagedFilesCount => _fileRows.Count(r => r.IsStaged);

        // ===== 构造函数 =====

        // 无参构造：供 XAML 实例化（RepositoryContentUserControl.axaml 内 <uc:CommitUserControl/>）
        public CommitUserControl() : this(serviceProvider: null)
        {
        }

        // IServiceProvider 构造：供 DI 解析 + task spec 要求的构造函数签名
        public CommitUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();
            InitializeCore();
        }

        // 共享初始化（两个构造函数共用）
        private void InitializeCore()
        {
            // 对照 WPF: StageFileUserControl 数据绑定初始化
            FileListItemsControl.ItemsSource = _fileRows;

            // 对照 WPF: base.Loaded += delegate { InitializeButtonHandlers(); InitializeKeyBindings(); ... }
            Loaded += OnLoaded;

            // 对照 WPF: CommitSubjectTextBox.TextChanged + CommitDescriptionTextBox.TextChanged
            //   spike 版用 AvaloniaEdit Document.TextChanged 替代
            if (CommitMessageTextBox?.Document != null)
            {
                CommitMessageTextBox.Document.TextChanged += CommitMessageTextBox_DocumentTextChanged;
            }

            // 对照 WPF: gridSplitter.DragCompleted → SaveGridColumnWidth（spike 暂不持久化列宽）

            RefreshSubjectLengthLimitToolTip();
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;
            // 对照 WPF Loaded: InitializeButtonHandlers / InitializeKeyBindings / RestoreGridColumnWidth /
            //   RefreshDescriptionFieldHeight / StageFileUserControl.RefreshUnstagedStatusLabel
            RestoreGridColumnWidth();
            UpdateCommitSection();
            UpdateSubjectLengthLimit();
        }

        // ===== 公共方法（对照 WPF）=====

        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl)
        //   注入父控件，并向下注入 FileDiffControl.RepositoryUserControl
        public void Initialize(object repositoryUserControl)
        {
            RepositoryUserControl = repositoryUserControl;
            if (CommitFileDiffControl != null)
            {
                CommitFileDiffControl.RepositoryUserControl = repositoryUserControl;
            }
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            // 对照 WPF: PreferencesLocalization.ApplyCurrent(this) → ServiceLocator.Localization
            RefreshSubjectLengthLimitToolTip();
            UpdateCommitButtonTitle();
            UpdateCommitWarningMessage();
        }

        // task spec: Refresh(ChangedFile[] changedFiles, string[] stagedFiles)
        //   刷新 staged/unstaged 文件列表（spike 简化版：用 ChangedFile.Staged + stagedFiles 双重判定）
        public void Refresh(ChangedFile[] changedFiles, string[] stagedFiles)
        {
            if (changedFiles == null)
            {
                changedFiles = Array.Empty<ChangedFile>();
            }
            _allChangedFiles = changedFiles;
            _refreshing = true;
            try
            {
                _fileRows.Clear();
                HashSet<string> stagedSet = stagedFiles != null
                    ? new HashSet<string>(stagedFiles)
                    : null;
                foreach (ChangedFile file in changedFiles)
                {
                    bool staged = stagedSet != null
                        ? stagedSet.Contains(file.Path)
                        : file.Staged;
                    _fileRows.Add(new FileRow
                    {
                        FilePath = file.Path,
                        File = file,
                        ChangeType = file.ChangeType,
                        StatusEmoji = GetStatusEmoji(file.ChangeType),
                        IsStaged = staged
                    });
                }
                ApplyFileFilter();
                UpdateStagedDiffStats();
                UpdateCommitSection();
            }
            finally
            {
                _refreshing = false;
            }
        }

        // 对照 WPF: public void Refresh(SubDomain domainsToRefresh = SubDomain.Status)
        //   spike 版用字符串占位 SubDomain；转发到刷新 UI 状态
        public void Refresh(object domainsToRefresh = null)
        {
            // 对照 WPF: RepositoryUserControl.InvalidateAndRefresh(domainsToRefresh, ...)
            OnRepositoryRefresh?.Invoke();
            UpdateCommitSection();
        }

        // 对照 WPF: public void StageSelectedFiles()
        //   暂存当前选中的未暂存文件（spike 版：StageFile(SelectedFile)）
        public void StageSelectedFiles()
        {
            if (StageJob != null) return;
            if (_selectedRow != null && !_selectedRow.IsStaged && _selectedRow.File != null)
            {
                StageFile(_selectedRow.File);
            }
        }

        // 对照 WPF: public void LoadCommitMessage()
        //   从 .git/COMMIT_EDITMSG 或草稿加载未提交的 commit message
        //   spike 版：GitModule 可访问时走真实 git；否则加载草稿
        public void LoadCommitMessage()
        {
            var gitModule = GitModule;
            if (gitModule != null)
            {
                Task.Run(() =>
                {
                    var result = new GetMergeCommitMessageGitCommand().Execute(gitModule);
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (result.Succeeded)
                        {
                            FullCommitMessage = result.Result ?? string.Empty;
                        }
                        else if (gitModule.Settings != null)
                        {
                            FullCommitMessage = gitModule.Settings.DraftMessage ?? string.Empty;
                        }
                        UpdateCommitSection();
                    });
                });
            }
            else if (gitModule?.Settings != null)
            {
                FullCommitMessage = gitModule.Settings.DraftMessage ?? string.Empty;
            }
        }

        // 对照 WPF: public void SaveCommitMessage()
        //   保存 commit message 草稿到 GitModule.Settings.DraftMessage
        public void SaveCommitMessage()
        {
            var gitModule = GitModule;
            if (gitModule?.Settings != null)
            {
                gitModule.Settings.DraftMessage = FullCommitMessage;
                gitModule.Settings.Save();
            }
        }

        // 对照 WPF: public void EraseSavedCommitMessage()
        public void EraseSavedCommitMessage()
        {
            var gitModule = GitModule;
            if (gitModule?.Settings != null)
            {
                gitModule.Settings.DraftMessage = string.Empty;
                gitModule.Settings.Save();
            }
        }

        // 对照 WPF: public void UpdateCommitMode()
        //   Amend 模式切换时：保存当前消息 + 加载 HEAD 消息（amend）或加载草稿（非 amend）
        public void UpdateCommitMode()
        {
            var gitModule = GitModule;
            if (gitModule == null)
            {
                UpdateCommitSection();
                return;
            }
            if (AmendMode)
            {
                SaveCommitMessage();
                Task.Run(() =>
                {
                    var result = new GetHeadMessageGitCommand().Execute(gitModule);
                    Dispatcher.UIThread.Post(() =>
                    {
                        FullCommitMessage = result?.Result ?? string.Empty;
                        UpdateCommitSection();
                    });
                });
            }
            else
            {
                LoadCommitMessage();
                UpdateCommitSection();
            }
        }

        // 对照 WPF: public void RefreshStageControls()
        //   StageJob != null 时禁用文件列表；否则启用
        public void RefreshStageControls()
        {
            bool enabled = StageJob == null;
            if (FileListItemsControl != null)
            {
                FileListItemsControl.IsEnabled = enabled;
            }
        }

        // 对照 WPF: public void UpdateCommitSection(bool updateWarningMessage = true)
        //   更新提交区状态：commit 消息框可用性 / Amend 复选框 / Commit 按钮标题+状态 / 警告消息
        public void UpdateCommitSection(bool updateWarningMessage = true)
        {
            bool fieldsAllowed = AreCommitFieldsAllowed;
            if (CommitMessageTextBox != null)
            {
                CommitMessageTextBox.IsReadOnly = !fieldsAllowed;
            }
            if (RecentCommitMessagesButton != null)
            {
                RecentCommitMessagesButton.IsEnabled = fieldsAllowed;
            }
            if (AmendCheckBox != null)
            {
                AmendCheckBox.IsEnabled = IsAmendAllowed;
            }
            UpdateCommitButtonTitle();
            UpdateCommitButtonState();
            if (updateWarningMessage)
            {
                UpdateCommitWarningMessage();
            }
        }

        // 对照 WPF: public void FocusCommitMessageField()
        public void FocusCommitMessageField()
        {
            CommitMessageTextBox?.Focus();
        }

        // 对照 WPF: public void ToggleShowIgnoredFiles()
        public void ToggleShowIgnoredFiles()
        {
            ShowIgnoredFiles = !ShowIgnoredFiles;
            OnRepositoryRefresh?.Invoke();
        }

        // ===== task spec 简化 API =====

        // task spec: SetCommitMessage(string message)
        public void SetCommitMessage(string message)
        {
            FullCommitMessage = message;
            UpdateCommitButtonState();
            UpdateSubjectLengthLimit();
            UpdateCommitWarningMessage();
        }

        // task spec: GetCommitMessage()
        public string GetCommitMessage()
        {
            return FullCommitMessage;
        }

        // task spec: StageFile(ChangedFile file)
        //   暂存单个文件：更新 FileRow.IsStaged + 调 git add（GitModule 可用时）
        public void StageFile(ChangedFile file)
        {
            if (file == null) return;
            FileRow row = _fileRows.FirstOrDefault(r => r.File != null && r.File.Path == file.Path);
            if (row != null)
            {
                row.IsStaged = true;
            }
            ExecuteStageCommand(new[] { file }, staged: true);
            UpdateStagedDiffStats();
            UpdateCommitSection();
        }

        // task spec: UnstageFile(ChangedFile file)
        public void UnstageFile(ChangedFile file)
        {
            if (file == null) return;
            FileRow row = _fileRows.FirstOrDefault(r => r.File != null && r.File.Path == file.Path);
            if (row != null)
            {
                row.IsStaged = false;
            }
            ExecuteStageCommand(new[] { file }, staged: false);
            UpdateStagedDiffStats();
            UpdateCommitSection();
        }

        // task spec: StageAll()
        public void StageAll()
        {
            ChangedFile[] unstaged = UnstagedFiles;
            if (unstaged.Length == 0) return;
            foreach (FileRow row in _fileRows)
            {
                row.IsStaged = true;
            }
            ExecuteStageCommand(unstaged, staged: true);
            UpdateStagedDiffStats();
            UpdateCommitSection();
        }

        // task spec: UnstageAll()
        public void UnstageAll()
        {
            ChangedFile[] staged = StagedFiles;
            if (staged.Length == 0) return;
            foreach (FileRow row in _fileRows)
            {
                row.IsStaged = false;
            }
            ExecuteStageCommand(staged, staged: false);
            UpdateStagedDiffStats();
            UpdateCommitSection();
        }

        // task spec: DiscardChanges(ChangedFile file)
        //   丢弃变更：从列表移除 + 调 git checkout --（GitModule 可用时）
        public void DiscardChanges(ChangedFile file)
        {
            if (file == null) return;
            FileRow row = _fileRows.FirstOrDefault(r => r.File != null && r.File.Path == file.Path);
            if (row != null)
            {
                _fileRows.Remove(row);
                if (_selectedRow == row)
                {
                    _selectedRow = null;
                }
            }
            ExecuteDiscardCommand(new[] { file });
            UpdateCommitSection();
        }

        // task spec: Commit()
        public void Commit()
        {
            ExecuteCommit(amend: AmendMode, commitAndPush: false);
        }

        // task spec: CommitAndPush()
        public void CommitAndPush()
        {
            ExecuteCommit(amend: AmendMode, commitAndPush: true);
        }

        // task spec: AmendCommit()
        public void AmendCommit()
        {
            AmendMode = true;
            ExecuteCommit(amend: true, commitAndPush: false);
        }

        // ===== 提交流程（对照 WPF Commands.Commit.Execute + JobQueue）=====
        //   WPF: JobQueue.Add("Commit", monitor => new CommitGitCommand().Execute(gitModule, message, amend, commitAndPush, monitor))
        //   spike: Task.Run + Dispatcher.UIThread.Post + JobMonitor
        private void ExecuteCommit(bool amend, bool commitAndPush)
        {
            if (CommittingInProgress) return;
            if (!IsCommitAllowed) return;

            string message = GetCommitMessage();
            var gitModule = GitModule;
            var monitor = new JobMonitor();

            CommittingInProgress = true;
            UpdateCommitSection();

            Task.Run(() =>
            {
                try
                {
                    if (gitModule != null && !monitor.IsCanceled)
                    {
                        // 对照 WPF: new CommitGitCommand().Execute(gitModule, message, amend, commitAndPush, monitor)
                        new CommitGitCommand().Execute(gitModule, message, amend, commitAndPush, monitor);
                    }
                }
                catch (Exception ex)
                {
                    monitor.Fail(ex.Message);
                }
                finally
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        CommittingInProgress = false;
                        if (!monitor.IsCanceled)
                        {
                            EraseSavedCommitMessage();
                            FullCommitMessage = string.Empty;
                        }
                        UpdateCommitSection();
                        // 对照 WPF: RepositoryUserControl.InvalidateAndRefresh
                        OnRepositoryRefresh?.Invoke();
                    });
                }
            });
        }

        // ===== Stage/Unstage/Discard git 命令执行（Task.Run + JobMonitor）=====
        private void ExecuteStageCommand(ChangedFile[] files, bool staged)
        {
            var gitModule = GitModule;
            if (gitModule == null || files == null || files.Length == 0) return;

            var monitor = new JobMonitor();
            StageJob = monitor;
            RefreshStageControls();

            Task.Run(() =>
            {
                try
                {
                    // 对照 WPF: new StageFileGitCommand().Execute / new UnstageGitCommand().Execute
                    if (staged)
                    {
                        new StageFileGitCommand().Execute(gitModule, files, monitor);
                    }
                    else
                    {
                        new UnstageGitCommand().Execute(gitModule, files, monitor);
                    }
                }
                catch (Exception ex)
                {
                    monitor.Fail(ex.Message);
                }
                finally
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        StageJob = null;
                        RefreshStageControls();
                        OnRepositoryRefresh?.Invoke();
                    });
                }
            });
        }

        private void ExecuteDiscardCommand(ChangedFile[] files)
        {
            var gitModule = GitModule;
            if (gitModule == null || files == null || files.Length == 0) return;

            var monitor = new JobMonitor();
            Task.Run(() =>
            {
                try
                {
                    // 对照 WPF: new DiscardFileChangesGitCommand().Execute(gitModule, files, monitor)
                    new DiscardFileChangesGitCommand().Execute(gitModule, files, monitor);
                }
                catch (Exception ex)
                {
                    monitor.Fail(ex.Message);
                }
                finally
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        OnRepositoryRefresh?.Invoke();
                    });
                }
            });
        }

        // ===== UI 更新方法（对照 WPF）=====

        // 对照 WPF: private bool AreCommitFieldsAllowed
        private bool AreCommitFieldsAllowed
        {
            get
            {
                if (StageJob != null) return false;
                if (CommittingInProgress) return false;
                return true;
            }
        }

        // 对照 WPF: private bool IsAmendAllowed() → isAmendAllowed()
        private bool IsAmendAllowed
        {
            get
            {
                if (StageJob != null) return false;
                if (CommittingInProgress) return false;
                return true;
            }
        }

        // 对照 WPF: private void UpdateCommitButtonTitle()
        //   按 staged 计数 + amend 模式更新 Commit 按钮标题
        private void UpdateCommitButtonTitle()
        {
            if (CommitButton == null) return;

            int stagedCount = StagedFilesCount;
            string title;
            if (AmendMode)
            {
                title = "Amend Last Commit";
            }
            else
            {
                switch (stagedCount)
                {
                    case 0:
                        title = "Commit";
                        break;
                    case 1:
                        title = "Commit 1 File";
                        break;
                    default:
                        title = $"Commit {stagedCount} Files";
                        break;
                }
            }
            // 对照 WPF: if (CommitAndPush) title += " and Push"
            if (ForkPlusSettings.Default.PushAutomaticallyOnCommit && !AmendMode)
            {
                title += " and Push";
            }
            CommitButton.Content = title;
        }

        // 对照 WPF: private void UpdateCommitButtonState()
        private void UpdateCommitButtonState()
        {
            if (CommitButton != null)
            {
                CommitButton.IsEnabled = IsCommitAllowed;
            }
        }

        // 对照 WPF: private void UpdateCommitWarningMessage()
        //   spike 版：commit message regex 校验（GitModule 可用时）+ 空消息提示
        private void UpdateCommitWarningMessage()
        {
            if (WarningTextBlock == null) return;

            var gitModule = GitModule;
            bool skipRegex = gitModule?.Settings?.SkipCommitMessage ?? false;
            if (!skipRegex && gitModule?.Settings != null)
            {
                string commitRegex = gitModule.Settings.CommitMessageRegex;
                if (string.IsNullOrWhiteSpace(commitRegex))
                {
                    commitRegex = ForkPlusSettings.Default.CommitMessageRegex;
                }
                // 对照 WPF: OpenAiService.MatchesCommitMessageRegex（spike 省略，仅做基本空校验）
            }
            if (IsCommitAllowed && string.IsNullOrWhiteSpace(FullCommitMessage))
            {
                WarningTextBlock.Text = "Commit message is empty";
            }
            else
            {
                WarningTextBlock.Text = string.Empty;
            }
        }

        // 对照 WPF: private void UpdateSubjectLengthLimit()
        //   72/50 规则：subject 行长度提示
        private void UpdateSubjectLengthLimit()
        {
            if (SubjectLengthLimitTextBlock == null) return;

            int lowLimit = ForkPlusSettings.Default.CommitSubjectLowLimit;
            int highLimit = ForkPlusSettings.Default.CommitSubjectHighLimit;
            string subject = GetSubjectLine();
            int length = subject?.Length ?? 0;

            if (length == 0)
            {
                SubjectLengthLimitTextBlock.IsVisible = false;
                return;
            }

            if (length > highLimit)
            {
                SubjectLengthLimitTextBlock.Foreground = TryGetBrush("CommitSublectLength.Error.ForegroundBrush");
            }
            else if (length > lowLimit)
            {
                SubjectLengthLimitTextBlock.Foreground = TryGetBrush("CommitSublectLength.Warning.ForegroundBrush");
            }
            else
            {
                SubjectLengthLimitTextBlock.Foreground = TryGetBrush("CommitSublectLength.OK.ForegroundBrush");
            }
            int remaining = lowLimit - length;
            SubjectLengthLimitTextBlock.Text = remaining.ToString();
            SubjectLengthLimitTextBlock.IsVisible = true;
        }

        // 对照 WPF: private void RefreshSubjectLengthLimitToolTip()
        private void RefreshSubjectLengthLimitToolTip()
        {
            if (SubjectLengthLimitTextBlock != null)
            {
                int lowLimit = ForkPlusSettings.Default.CommitSubjectLowLimit;
                global::Avalonia.Controls.ToolTip.SetTip(SubjectLengthLimitTextBlock,
                    $"The recommended subject line should be {lowLimit} characters or less");
            }
        }

        // 对照 WPF: private void UpdateStagedDiffStats()
        //   spike 版：显示 staged 文件计数（git diff --cached --numstat 需 GitModule，spike 用计数替代）
        private void UpdateStagedDiffStats()
        {
            if (StagedDiffStatsTextBlock == null) return;
            int stagedCount = StagedFilesCount;
            int totalCount = _fileRows.Count;
            StagedDiffStatsTextBlock.Text = totalCount > 0
                ? $"{stagedCount}/{totalCount} staged"
                : string.Empty;
        }

        // ===== 文件列表过滤（对照 WPF StageFileUserControl FilterTextBox）=====
        private void ApplyFileFilter()
        {
            string filter = FileFilterTextBox?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filter))
            {
                // 无过滤：显示全部（_fileRows 已包含全部，无需变动）
                return;
            }
            // spike 版：过滤仅影响显示，不修改 _fileRows 源数据
            // 由于 ItemsControl 直接绑定 _fileRows，过滤需移除不匹配项。
            // 为保留源数据，用 _allChangedFiles 重建匹配的 _fileRows
            string lowerFilter = filter.ToLowerInvariant();
            List<FileRow> toRemove = _fileRows
                .Where(r => r.File == null || (r.File.Path != null && !r.File.Path.ToLowerInvariant().Contains(lowerFilter)))
                .ToList();
            foreach (FileRow row in toRemove)
            {
                _fileRows.Remove(row);
            }
        }

        // ===== 选中文件 diff 预览（对照 WPF UpdateDiff）=====
        //   spike 版：GitModule 可用时调 git diff；否则设置 diff 占位
        private void UpdateDiff(ChangedFile file)
        {
            if (file == null || file.IsDirectory)
            {
                if (CommitFileDiffControl != null)
                {
                    CommitFileDiffControl.Content = null;
                }
                return;
            }
            var gitModule = GitModule;
            if (gitModule == null)
            {
                // spike：无 GitModule 时设置占位 Content（驱动 FileDiffControl 显示 fallback）
                if (CommitFileDiffControl != null)
                {
                    CommitFileDiffControl.Content = "Fallback";
                }
                return;
            }
            // 对照 WPF: LoadWorkingDirectoryDiff（spike 版用 Task.Run + Dispatcher.UIThread.Post）
            Task.Run(() =>
            {
                // 对照 WPF: new GetWorkingDirectoryFileChangesGitCommand().Execute(...)
                //   spike 版暂不调真实 git diff（需 DiffContent + PatchParser），仅标记已选
                Dispatcher.UIThread.Post(() =>
                {
                    if (CommitFileDiffControl != null)
                    {
                        CommitFileDiffControl.Content = "Text";
                    }
                });
            });
        }

        // ===== 事件处理 =====

        // 对照 WPF: CommitButton_Click → Commands.Commit.Execute(this, CommitAndPush)
        private void CommitButton_Click(object sender, RoutedEventArgs e)
        {
            Commit();
        }

        // 对照 WPF: AmendCheckBox_Changed → UpdateCommitMode + UpdateStagedDiffStats + Refresh
        private void AmendCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (!DontRefreshOnAmend)
            {
                UpdateCommitMode();
                UpdateStagedDiffStats();
                Refresh();
            }
        }

        // 对照 WPF: CommitSubjectTextBox_TextChanged + CommitDescriptionTextBox_TextChanged
        //   spike 版：AvaloniaEdit Document.TextChanged 替代
        private void CommitMessageTextBox_DocumentTextChanged(object sender, EventArgs e)
        {
            if (_suppressTextChanged) return;
            UpdateCommitButtonState();
            UpdateSubjectLengthLimit();
            UpdateCommitWarningMessage();
        }

        // 对照 WPF: ClearCommitMessageButton_Click
        private void ClearCommitMessageButton_Click(object sender, RoutedEventArgs e)
        {
            FullCommitMessage = string.Empty;
            EraseSavedCommitMessage();
            UpdateCommitButtonState();
            UpdateSubjectLengthLimit();
            UpdateCommitWarningMessage();
            CommitMessageTextBox?.Focus();
        }

        // task spec: Co-authored-by 模板插入
        private void InsertCoAuthoredButton_Click(object sender, RoutedEventArgs e)
        {
            string current = FullCommitMessage ?? string.Empty;
            if (!current.EndsWith("\n"))
            {
                current += "\n";
            }
            current += "\nCo-authored-by: Name <email@example.com>\n";
            FullCommitMessage = current;
        }

        // 对照 WPF: RecentCommitMessagesDropDownButton_Click（spike 版不实现下拉，仅日志）
        private void RecentCommitMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            // spike 版：最近消息下拉需 AiAgent/OpenAiService/GetRecentRevisionMessagesGitCommand，
            // 暂不迁移（对照 WPF RecentCommitMessagesContextMenu_Opened）
        }

        // 对照 WPF: CommitSettingsDropDownButton_Click（spike 版不实现下拉）
        private void CommitSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // spike 版：commit 设置下拉需 DropDownButton + ContextMenu，暂不迁移
        }

        // task spec: 全部暂存按钮
        private void StageAllButton_Click(object sender, RoutedEventArgs e)
        {
            StageAll();
        }

        // task spec: 全部取消暂存按钮
        private void UnstageAllButton_Click(object sender, RoutedEventArgs e)
        {
            UnstageAll();
        }

        // task spec: 丢弃选中文件变更
        private void DiscardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRow != null && _selectedRow.File != null)
            {
                DiscardChanges(_selectedRow.File);
            }
        }

        // 文件过滤搜索框（对照 WPF StageFileUserControl FilterTextBox）
        private void FileFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // spike 版：过滤时从 _allChangedFiles 重建 _fileRows
            string filter = FileFilterTextBox?.Text ?? string.Empty;
            bool wasFiltered = _fileRows.Count < _allChangedFiles.Length;
            if (string.IsNullOrWhiteSpace(filter) && wasFiltered)
            {
                // 清空过滤：重建全部
                Refresh(_allChangedFiles, StagedFiles.Select(f => f.Path).ToArray());
                return;
            }
            ApplyFileFilter();
        }

        // 文件行复选框点击（stage/unstage 单个文件）
        private void FileRowStaged_Click(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            if (sender is CheckBox cb && cb.DataContext is FileRow row && row.File != null)
            {
                bool newStaged = cb.IsChecked ?? false;
                row.IsStaged = newStaged;
                if (newStaged)
                {
                    ExecuteStageCommand(new[] { row.File }, staged: true);
                }
                else
                {
                    ExecuteStageCommand(new[] { row.File }, staged: false);
                }
                UpdateStagedDiffStats();
                UpdateCommitSection();
            }
        }

        // 文件行选中（PointerPressed 对照 WPF StageFileUserControl.SelectionChanged）
        private void FileRow_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is FileRow row)
            {
                _selectedRow = row;
                UpdateDiff(row.File);
            }
        }

        // ===== 私有辅助方法 =====

        // 对照 WPF: private static void SplitCommitMessageForFields(string fullMessage, out string subject, out string description)
        private static void SplitCommitMessageForFields(string fullMessage, out string subject, out string description)
        {
            string text = (fullMessage ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
            int lineBreakIndex = text.IndexOf('\n');
            if (lineBreakIndex < 0)
            {
                subject = text.Trim('\n');
                description = string.Empty;
                return;
            }
            subject = text.Substring(0, lineBreakIndex).Trim('\n');
            description = text.Substring(lineBreakIndex + 1).TrimStart('\n');
        }

        // 从 FullCommitMessage 提取 subject 行（第一行）
        private string GetSubjectLine()
        {
            SplitCommitMessageForFields(FullCommitMessage, out string subject, out _);
            return subject;
        }

        // 对照 WPF: private void RestoreGridColumnWidth()（spike 暂不持久化，保留方法签名）
        private void RestoreGridColumnWidth()
        {
            // 对照 WPF: CommitGrid.ColumnDefinitions[0].Width = new GridLength(ForkPlusSettings.Default.CommitViewColumnWidth, GridUnitType.Pixel)
            // spike 版暂不持久化列宽
        }

        // 对照 WPF: private void SaveGridColumnWidth()（spike 暂不持久化）
        private void SaveGridColumnWidth()
        {
            // spike 版暂不持久化列宽
        }

        // 对照 WPF: Application.Current.TryFindResource(key) as Brush
        //   Avalonia: Application.Current!.TryGetResource(key, null, out var v)
        private IBrush TryGetBrush(string key)
        {
            if (Application.Current != null && Application.Current.TryGetResource(key, null, out var v) && v is IBrush brush)
            {
                return brush;
            }
            return null;
        }

        // ChangeType → emoji 映射（对照 task spec: M=📝 / A=✨ / D=🗑 / R=🔀 / Untracked=❓）
        private static string GetStatusEmoji(ChangeType changeType)
        {
            switch (changeType)
            {
                case ChangeType.Modified:    return "\U0001F4DD"; // 📝 M
                case ChangeType.Added:       return "\U0001F4A8";  // ✨ A
                case ChangeType.Deleted:     return "\U0001F5D1"; // 🗑 D
                case ChangeType.Renamed:     return "\U0001F500"; // 🔀 R
                case ChangeType.Copied:      return "\U0001F4CB"; // 📋 C
                case ChangeType.TypeChanged: return "\U0001F504"; // 🔄 T
                case ChangeType.Unmerged:    return "\u26A0";     // ⚠ U (冲突)
                case ChangeType.Untracked:   return "\u2753";    // ❓ 未跟踪
                case ChangeType.Ignored:     return "\U0001F6AB"; // 🚫 已忽略
                case ChangeType.Unknown:     return "\u2754";     // ❔ 未知
                default:                     return "\u2754";     // ❔
            }
        }
    }
}
