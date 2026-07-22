using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.Undo;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 5.1 完整迁移版：Avalonia 版 RepositoryUserControl。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RepositoryUserControl.xaml.cs（1392 行）：
    //   - 33 个公共方法（RefreshRepositoryTitle/UpdateRepositoryData/UpdateRepositoryStatus/
    //     NormalizeRepositoryStatusForDisplay/NormalizeChangedFilesForDisplay/RefreshToolbarBadges/
    //     UpdateIsDirtyState/OpenRepository/ApplyLocalization/SidebarRevealActiveBranch/
    //     SidebarActivateRepositoryTab/SidebarActivateSearchTab/ActivateCommitView/ActivateRevisionView/
    //     SelectAndScrollIntoView/SelectRevision/SelectRevisions(2 重载)/FetchNextRevisionPage/
    //     FetchUntilFindContextSearchMatch/CancelActiveFetchRevisionsJobs/FocusSelectedRevision/
    //     EraseSavedCommitMessage/CollapseAllMerges/ExpandAllMerges/ShowRevisionDetails/Invalidate/
    //     ResetSubdomains/InvalidateAndRefresh(2 重载)/UncheckAmendCheckBox/RaiseUndoRedoStateChanged/
    //     AddUndoable(2 重载)/Undo/Redo）
    //   - 6 个私有字段（_isDirty/_layoutInitialized/_invalidatedSubdomains/_viewMode/
    //     _activeFetchRevisionsUntilShaJob/_activeFetchRevisionsNextPageJob）
    //   - 延迟初始化模式：EnsureLayoutInitialized() 在首次 InvalidateAndRefresh 时
    //     创建 Sidebar + Content + NotificationBar 装入对应容器
    //   - Undo/Redo 子系统（v3.0.0+，~390 行）：TakeSnapshot/IsWorkingTreeDirty/AddUndoable/Undo/Redo/
    //     ConfirmAndStashBeforeRestore/EnsureStashedIfNeeded/ShouldPromptForPushedCommits/
    //     ConfirmPushedUndo/ForcePushCurrentBranch/ShowRestoreFailureAsync
    //   - NotificationCenter 事件订阅（RepositoryNameChanged/RepositoryColorChanged/
    //     UpdateRepoStatusAutomaticallyChanged）
    //   - SidebarGridSplitter.DragCompleted 持久化列宽
    //   - 依赖：RepositoryManager.Instance / ForkPlusSettings.Default / NotificationCenter.Current /
    //     MainWindow.Instance / GitModule / RepositoryData / RepositoryStatus / CommitGraphCache
    //
    // spike 简化策略（关键解耦点）：
    //   - RepositoryManager.Instance → 注入回调 FindRepositoryNameCallback / AddOrUpdateLastOpenedCallback
    //   - MainWindow.Instance → 注入回调 RefreshToolbarBadgesCallback
    //   - NotificationCenter.Current → 本地 C# 事件 RepositoryNameChangedEvent /
    //     RepositoryColorChangedEvent / UpdateRepoStatusAutomaticallyChangedEvent +
    //     RepositoryUserControlIsDirtyChanged/TitleChanged/ColorChanged/DataUpdated/StatusUpdated
    //   - RefreshRepositoryCommand（WPF-only）→ 注入回调 RefreshRepositoryCallback
    //   - PreferencesLocalization → ServiceLocator.Localization
    //   - MessageBox.Show → ServiceLocator.Dialogs.ShowMessage
    //   - ErrorWindow → ServiceLocator.Dialogs.ShowError
    //   - Dispatcher.Invoke / Dispatcher.Async → Dispatcher.UIThread.Post
    //   - Visibility.Collapsed/Visible → IsVisible = false/true
    //   - RepositoryViewMode 枚举（WPF-only）→ string（"RevisionViewMode" / "CommitViewMode"）
    //   - RevisionSelector / RevisionContextSearch / NoUIAutomationListView.SelectOptions（WPF-only）→ object 占位
    //   - GetRevisionStorageGitCommand / RevisionStorage / RevisionsDataSource（WPF-only）→ 暂不迁移
    //     FetchNextRevisionPage / FetchUntilFindContextSearchMatch（JobQueue + 异步补抓链）
    //
    // 构造函数签名保持 (IServiceProvider serviceProvider)，与已注册的 DI 调用方兼容。
    // 命名空间保持 ForkPlus.Avalonia.Views.UserControls，调用方零改动切换。
    public partial class RepositoryUserControl : UserControl
    {
        // ===== 公共字段（对照 WPF）=====

        public readonly TempFileManager TempFileManager = new TempFileManager();

        public readonly JobQueue JobQueue = new JobQueue();

        // 本仓库的 Undo/Redo 历史栈（v3.0.0 新增）
        public readonly UndoRedoStack UndoRedoStack = new UndoRedoStack();

        // Undo/Redo 状态变化时触发，UI 工具栏订阅以刷新按钮可用性
        public event EventHandler UndoRedoStateChanged;

        // ===== 私有字段（对照 WPF 6 个私有字段）=====

        private readonly IServiceProvider _serviceProvider;
        private bool _isDirty;
        private bool _layoutInitialized;
        private SubDomain _invalidatedSubdomains = SubDomain.All;
        private string _viewMode = RepositoryContentUserControl.RevisionViewMode;
        private Job _activeFetchRevisionsUntilShaJob;
        private Job _activeFetchRevisionsNextPageJob;

        // spike 内部引用（对照 WPF Sidebar/Content/NotificationBar 属性）
        private SidebarUserControl _sidebar;
        private RepositoryContentUserControl _content;
        private NotificationBarUserControl _notificationBar;

        // ===== 公共属性（对照 WPF）=====

        public RepositoryData RepositoryData { get; private set; }

        public RepositoryStatus RepositoryStatus { get; private set; }

        public GitModule GitModule { get; private set; }

        public CommitGraphCache CommitGraphCache { get; private set; }

        public string RepositoryName { get; private set; }

        public string ParentRepositoryName { get; private set; }

        public string RepositoryTitle { get; private set; }

        public bool IsDirty
        {
            get
            {
                if (_isDirty)
                {
                    return ForkPlusSettings.Default.AutomaticStatusUpdateInterval > 0;
                }
                return false;
            }
            set
            {
                _isDirty = value;
            }
        }

        public RepositoryColor RepositoryColor { get; private set; }

        public SubDomain InvalidatedSubdomains => _invalidatedSubdomains;

        // 对照 WPF: public RepositoryViewMode ViewMode { get; private set; }
        //   WPF setter 调用 Content.SetRepositoryViewMode + Sidebar.SetRepositoryViewMode + NotificationBar.Refresh
        // spike: RepositoryViewMode 枚举（WPF-only）→ string（"RevisionViewMode" / "CommitViewMode"）
        public string ViewMode
        {
            get { return _viewMode; }
            private set
            {
                if (_viewMode != value)
                {
                    _viewMode = value;
                    _content?.SetRepositoryViewMode(_viewMode);
                    _sidebar?.SetRepositoryViewMode(_viewMode);
                    _notificationBar?.Refresh();
                }
            }
        }

        public bool ShowReflogInRevisionList { get; set; }

        // 对照 WPF: public SidebarUserControl Sidebar { get; private set; }
        public SidebarUserControl Sidebar => _sidebar;

        // 对照 WPF: public new RepositoryContentUserControl Content { get; private set; }
        //   用 new 隐藏基类 UserControl.Content（WPF 同样用 new）
        public new RepositoryContentUserControl Content => _content;

        // ===== spike 注入回调（替代 WPF RepositoryManager.Instance / MainWindow.Instance /
        //                       NotificationCenter.Current / RefreshRepositoryCommand 依赖）=====

        // 对照 WPF: RepositoryManager.Instance.FindRepositoryName(path)
        //   返回 null 表示路径不在仓库列表中（spike 调用方注入 RepositoryManager 等价实现）
        public Func<string, string> FindRepositoryNameCallback { get; set; }

        // 对照 WPF: RepositoryManager.Instance.AddOrUpdateLastOpened(gitModule)
        //   返回 (Name, Color) 元组替代 WPF RepositoryManager.Repository 结构
        public Func<GitModule, (string Name, RepositoryColor Color)> AddOrUpdateLastOpenedCallback { get; set; }

        // 对照 WPF: MainWindow.Instance.Toolbar.RefreshPullPushBadges(upstreamStatus)
        //   spike 简化：直接传 this 让调用方决定刷新哪些 badges
        public Action<RepositoryUserControl> RefreshToolbarBadgesCallback { get; set; }

        // 对照 WPF: RefreshRepositoryCommand.Execute(this, shas, select, priority)
        //   spike 简化：调用方注入完整刷新链实现（JobQueue + UpdateRepositoryData/Status）
        public Action<RepositoryUserControl, SubDomain, object, string> RefreshRepositoryCallback { get; set; }

        // 对照 WPF: NotificationCenter.Current.RaiseRepositoryUserControlIsDirtyChanged /
        //           RaiseRepositoryUserControlTitleChanged / RaiseRepositoryUserControlColorChanged /
        //           RaiseRepositoryDataUpdated / RaiseRepositoryStatusUpdated
        //   spike 简化：用本地事件替代全局 NotificationCenter 广播
        public event EventHandler<RepositoryUserControl> RepositoryUserControlIsDirtyChanged;
        public event EventHandler<RepositoryUserControl> RepositoryUserControlTitleChanged;
        public event EventHandler<RepositoryUserControl> RepositoryUserControlColorChanged;
        public event EventHandler<RepositoryUserControl> RepositoryDataUpdated;
        public event EventHandler<RepositoryUserControl> RepositoryStatusUpdated;

        // 对照 WPF: NotificationCenter.Current RepositoryNameChanged / RepositoryColorChanged /
        //           UpdateRepoStatusAutomaticallyChanged 订阅
        //   spike 简化：用本地事件替代；由调用方（MainWindow）转发
        public event EventHandler<string> RepositoryNameChangedEvent;
        public event EventHandler<string> RepositoryColorChangedEvent;
        public event EventHandler<int> UpdateRepoStatusAutomaticallyChangedEvent;

        // ===== 构造函数（保持 (IServiceProvider serviceProvider) 签名）=====

        public RepositoryUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            _layoutInitialized = false;
            _viewMode = RepositoryContentUserControl.RevisionViewMode;

            // 对照 WPF: SidebarGridSplitter.DragCompleted += delegate { SaveSidebarColumnWidth(); };
            // Avalonia 11: GridSplitter 继承自 Thumb，Thumb.DragCompleted 为路由事件。
            // 用 AddHandler 订阅 routed event（适用于 Avalonia 11）。
            if (SidebarGridSplitter != null)
            {
                SidebarGridSplitter.AddHandler(Thumb.DragCompletedEvent,
                    (EventHandler<global::Avalonia.Input.VectorEventArgs>)((s, e) => SaveSidebarColumnWidth()));
            }
        }

        // ===== OpenRepository / RefreshRepositoryTitle / ApplyLocalization =====

        // 对照 WPF: private string FindParentRepositoryName(GitModule gitModule)
        private string FindParentRepositoryName(GitModule gitModule)
        {
            if (gitModule == null)
            {
                return null;
            }
            if (gitModule.Type == ModuleType.Submodule)
            {
                string parentRepoPath = gitModule.ParentRepoPath;
                if (parentRepoPath == null)
                {
                    return null;
                }
                return FindRepositoryNameCallback != null
                    ? (FindRepositoryNameCallback(parentRepoPath) ?? Path.GetFileName(parentRepoPath))
                    : Path.GetFileName(parentRepoPath);
            }
            if (gitModule.Type == ModuleType.Worktree)
            {
                string commonGitDir = gitModule.CommonGitDir;
                if (commonGitDir == null)
                {
                    return null;
                }
                if (Path.GetFileName(commonGitDir) != ".git")
                {
                    return Path.GetFileName(commonGitDir);
                }
                string directoryName = Path.GetDirectoryName(commonGitDir);
                return FindRepositoryNameCallback != null
                    ? (FindRepositoryNameCallback(directoryName) ?? Path.GetFileName(directoryName))
                    : Path.GetFileName(directoryName);
            }
            return null;
        }

        // 对照 WPF: public void RefreshRepositoryTitle()
        public void RefreshRepositoryTitle()
        {
            GitModule gitModule = GitModule;
            if (gitModule == null)
            {
                RepositoryTitle = "";
                return;
            }
            string text = FindParentRepositoryName(gitModule);
            if (text != null)
            {
                string text2 = text + ": " + gitModule.RepositoryName;
                if (gitModule.Type == ModuleType.Worktree)
                {
                    string text3 = FindSiblingWorktreeWithSameName(gitModule);
                    if (text3 != null)
                    {
                        string item = PathHelper.FindFirstDifferentComponent(gitModule.Path, text3).Item1;
                        if (item != null)
                        {
                            RepositoryTitle = text2 + " (" + item + ")";
                            return;
                        }
                    }
                }
                RepositoryTitle = text2;
                return;
            }
            // 对照 WPF: RepositoryManager.Instance.Repositories.FirstItemStruct(...) → alias 查找
            // spike: FindRepositoryNameCallback 仅返回名字，不返回 alias；
            //        alias 检索逻辑由调用方在 callback 中实现（spike 简化：直接走 fallback）
            RepositoryTitle = gitModule.RepositoryName;
        }

        // 对照 WPF: public void UpdateRepositoryData(RepositoryData repositoryData, RevisionContextSearch? contextSearch, RevisionSelector select)
        //   spike: RevisionContextSearch / RevisionSelector 为 WPF-only 类型，用 object 占位
        public void UpdateRepositoryData(RepositoryData repositoryData, object contextSearch, object select)
        {
            RepositoryData oldData = RepositoryData;
            _sidebar?.UpdateRepositoryData(repositoryData);
            RepositoryData = repositoryData;
            _content?.RefreshRevisionItems(oldData, repositoryData, contextSearch, select);
            RefreshToolbarBadges();
            RepositoryDataUpdated?.Invoke(this, this);
        }

        // 对照 WPF: public void UpdateRepositoryStatus(RepositoryStatus repositoryStatus)
        public void UpdateRepositoryStatus(RepositoryStatus repositoryStatus)
        {
            if (RepositoryStateChanged(RepositoryStatus, repositoryStatus))
            {
                _content?.CommitUserControl?.EraseSavedCommitMessage();
                _content?.CommitUserControl?.LoadCommitMessage();
            }
            RepositoryStatus = repositoryStatus;
            RepositoryStatus displayStatus = NormalizeRepositoryStatusForDisplay(repositoryStatus);
            _sidebar?.UpdateRepositoryStatus(displayStatus);
            UpdateIsDirtyState(displayStatus != null
                && displayStatus.ChangedFiles != null
                && displayStatus.ChangedFiles.Length != 0);
            RepositoryStatusUpdated?.Invoke(this, this);
            _notificationBar?.Refresh();
        }

        // 对照 WPF: public RepositoryStatus NormalizeRepositoryStatusForDisplay(RepositoryStatus repositoryStatus)
        public RepositoryStatus NormalizeRepositoryStatusForDisplay(RepositoryStatus repositoryStatus)
        {
            if (repositoryStatus == null)
            {
                return null;
            }
            ChangedFile[] changedFiles = NormalizeChangedFilesForDisplay(repositoryStatus.ChangedFiles);
            if (changedFiles.Length == repositoryStatus.ChangedFiles.Length)
            {
                return repositoryStatus;
            }
            return new RepositoryStatus(repositoryStatus.RepositoryState, CountDistinctChangedFiles(changedFiles), changedFiles);
        }

        // 对照 WPF: public ChangedFile[] NormalizeChangedFilesForDisplay(ChangedFile[] changedFiles)
        //   WPF: 调用 ChangedFilesDisplayNormalizer.NormalizeForDisplay + GitMmUserControl 过滤子仓入口变更
        //   spike: GitMmUserControl / ChangedFilesDisplayNormalizer（WPF-only）未迁移，直接返回原数组
        public ChangedFile[] NormalizeChangedFilesForDisplay(ChangedFile[] changedFiles)
        {
            return changedFiles ?? new ChangedFile[0];
        }

        // 对照 WPF: private static int CountDistinctChangedFiles(ChangedFile[] changedFiles)
        private static int CountDistinctChangedFiles(ChangedFile[] changedFiles)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (ChangedFile changedFile in changedFiles)
            {
                paths.Add(changedFile.Path);
            }
            return paths.Count;
        }

        // 对照 WPF: public void RefreshToolbarBadges()
        //   WPF: 检查 MainWindow.Instance?.TabManager.ActiveRepositoryUserControl == this，
        //        读 RepositoryData.References.ActiveBranch → UpstreamStatus → Toolbar.RefreshPullPushBadges
        //   spike: 注入回调，调用方决定是否是当前活动 tab + 如何刷新 badges
        public void RefreshToolbarBadges()
        {
            RefreshToolbarBadgesCallback?.Invoke(this);
        }

        // 对照 WPF: public void UpdateIsDirtyState(bool newIsDirtyState)
        public void UpdateIsDirtyState(bool newIsDirtyState)
        {
            IsDirty = newIsDirtyState;
            RepositoryUserControlIsDirtyChanged?.Invoke(this, this);
        }

        // 对照 WPF: public void OpenRepository(GitModule gitModule)
        public void OpenRepository(GitModule gitModule)
        {
            // 无论 gitModule 是否为 null，都先确保 UI 布局初始化
            // （装入 Sidebar + RepositoryContent + NotificationBar），
            // 这样即使没有打开仓库，用户也能看到完整的界面骨架。
            EnsureLayoutInitialized();

            // spike 阶段可能传 null（无真实 repository），设置 null 后返回。
            // 真实仓库打开流程：File → Open Repository → 创建 GitModule → OpenRepository(module)。
            GitModule = gitModule;
            if (gitModule == null)
            {
                _sidebar?.RefreshTitle();
                return;
            }
            CommitGraphCache = new CommitGraphCache(gitModule);
            if (gitModule.Type == ModuleType.Submodule)
            {
                RepositoryName = gitModule.RepositoryName;
                string parentRepoPath = gitModule.ParentRepoPath;
                if (parentRepoPath != null)
                {
                    ParentRepositoryName = FindRepositoryNameCallback != null
                        ? (FindRepositoryNameCallback(parentRepoPath) ?? gitModule.ParentRepositoryName)
                        : gitModule.ParentRepositoryName;
                }
                else
                {
                    ParentRepositoryName = gitModule.ParentRepositoryName;
                }
            }
            else if (gitModule.Type == ModuleType.Worktree)
            {
                RepositoryName = gitModule.RepositoryName;
                string commonGitDir = gitModule.CommonGitDir;
                if (commonGitDir != null)
                {
                    if (Path.GetFileName(commonGitDir) != ".git")
                    {
                        string directoryName = Path.GetDirectoryName(commonGitDir);
                        ParentRepositoryName = FindRepositoryNameCallback != null
                            ? (FindRepositoryNameCallback(directoryName) ?? Path.GetFileName(directoryName))
                            : Path.GetFileName(directoryName);
                    }
                    else
                    {
                        string directoryName2 = Path.GetDirectoryName(commonGitDir);
                        ParentRepositoryName = FindRepositoryNameCallback != null
                            ? (FindRepositoryNameCallback(directoryName2) ?? Path.GetFileName(directoryName2))
                            : Path.GetFileName(directoryName2);
                    }
                }
                else
                {
                    ParentRepositoryName = null;
                }
            }
            else
            {
                // 对照 WPF: RepositoryManager.Repository r = RepositoryManager.Instance.AddOrUpdateLastOpened(gitModule);
                //           RepositoryName = r.Name(); RepositoryColor = r.Color;
                // spike: 注入回调返回 (Name, Color) 元组
                if (AddOrUpdateLastOpenedCallback != null)
                {
                    var r = AddOrUpdateLastOpenedCallback(gitModule);
                    RepositoryName = r.Name;
                    RepositoryColor = r.Color;
                }
                else
                {
                    RepositoryName = gitModule.RepositoryName;
                    RepositoryColor = RepositoryColor.None;
                }
                ParentRepositoryName = null;
            }
            _sidebar?.RefreshTitle();
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            _sidebar?.ApplyLocalization();
            _sidebar?.RefreshTitle();
            _content?.ApplyLocalization();
        }

        // ===== Sidebar 转发方法（对照 WPF）=====

        // 对照 WPF: public void SidebarRevealActiveBranch()
        public void SidebarRevealActiveBranch()
        {
            _sidebar?.RevealActiveBranch();
        }

        // 对照 WPF: public void SidebarActivateRepositoryTab()
        public void SidebarActivateRepositoryTab()
        {
            _sidebar?.ActivateRepositoryTab();
        }

        // 对照 WPF: public void SidebarActivateSearchTab()
        public void SidebarActivateSearchTab()
        {
            _sidebar?.ActivateSearchTab();
        }

        // ===== Content / ViewMode 切换（对照 WPF）=====

        // 对照 WPF: public void ActivateCommitView(bool focusCommitSubject = false)
        public void ActivateCommitView(bool focusCommitSubject = false)
        {
            bool isVisible = _content?.CommitView?.IsVisible ?? false;
            ViewMode = RepositoryContentUserControl.CommitViewMode;
            // 对照 WPF: if (isVisible || focusCommitSubject) Content.CommitUserControl.FocusCommitMessageField();
            //           else Content.CommitUserControl.StageFileUserControl.FocusActiveListView();
            // spike: StageFileUserControl 已内联到 CommitUserControl，无对应 FocusActiveListView 方法
            if (isVisible || focusCommitSubject)
            {
                _content?.CommitUserControl?.FocusCommitMessageField();
            }
        }

        // 对照 WPF: public void ActivateRevisionView()
        public void ActivateRevisionView()
        {
            ViewMode = RepositoryContentUserControl.RevisionViewMode;
        }

        // 对照 WPF: public bool SelectAndScrollIntoView(RevisionSelector selector)
        //   spike: RevisionSelector 为 WPF-only，用 object 占位；
        //         RevisionListViewUserControl.Select(selector, options) 未迁移，返回 false 占位
        public bool SelectAndScrollIntoView(object selector)
        {
            return false;
        }

        // 对照 WPF: public void SelectRevision(Sha sha, string filePath = null)
        public void SelectRevision(Sha sha, string filePath = null)
        {
            SelectRevisions(new Sha[] { sha }, (object)3, filePath);
        }

        // 对照 WPF: public void SelectRevisions(IReadOnlyList<Sha> shas, NoUIAutomationListView.SelectOptions selectOptions, string filePath)
        //   spike: NoUIAutomationListView.SelectOptions（WPF-only）→ object 占位
        public void SelectRevisions(IReadOnlyList<Sha> shas, object selectOptions = null, string filePath = null)
        {
            _content?.SelectRevisions(shas, selectOptions, filePath);
        }

        // 对照 WPF: public void SelectRevisions(IReadOnlyList<Sha> shas, bool fetchIfNeeded)
        //   spike: 完整 fetch-until-sha 流程（JobQueue + GetRevisionStorageGitCommand.FetchUntil +
        //         ExpandContextSearch + Dispatcher.Async）依赖 RevisionStorage/RevisionContextSearch 等
        //         WPF-only 类型，spike 简化为直接转发给 Content.SelectRevisions（不实现 fetchIfNeeded 异步补抓）
        public void SelectRevisions(IReadOnlyList<Sha> shas, bool fetchIfNeeded)
        {
            if (GitModule == null || RepositoryData == null || CommitGraphCache == null)
            {
                return;
            }
            CancelActiveFetchRevisionsJobs();
            if (shas == null || shas.Count == 0)
            {
                return;
            }
            _content?.SelectRevisions(shas, null, null);
        }

        // 对照 WPF: public void FetchNextRevisionPage()
        //   spike: GetRevisionStorageGitCommand / RevisionContextSearch / RevisionsDataSource 等
        //         WPF-only 类型未迁移，暂不实现（调用方需通过 RefreshRepositoryCallback 触发全量刷新）
        public void FetchNextRevisionPage()
        {
        }

        // 对照 WPF: public void FetchUntilFindContextSearchMatch(int selectedRow)
        //   spike: RevisionContextSearch / RevisionsDataSource.NextContextSearchMatch 等
        //         WPF-only 类型未迁移，暂不实现
        public void FetchUntilFindContextSearchMatch(int selectedRow)
        {
        }

        // 对照 WPF: public void CancelActiveFetchRevisionsJobs()
        public void CancelActiveFetchRevisionsJobs()
        {
            _activeFetchRevisionsUntilShaJob?.Monitor.Cancel();
            _activeFetchRevisionsUntilShaJob = null;
            _activeFetchRevisionsNextPageJob?.Monitor.Cancel();
            _activeFetchRevisionsNextPageJob = null;
        }

        // 对照 WPF: public void FocusSelectedRevision()
        //   spike: RevisionListViewUserControl.FocusSelectedItem 未迁移，跳过
        public void FocusSelectedRevision()
        {
        }

        // 对照 WPF: public void EraseSavedCommitMessage()
        public void EraseSavedCommitMessage()
        {
            _content?.CommitUserControl?.EraseSavedCommitMessage();
            if (_content?.CommitUserControl != null)
            {
                _content.CommitUserControl.FullCommitMessage = "";
            }
        }

        // 对照 WPF: public void CollapseAllMerges()
        //   spike: RevisionListViewUserControl.CollapseAll 未迁移，跳过
        public void CollapseAllMerges()
        {
        }

        // 对照 WPF: public void ExpandAllMerges()
        //   spike: RevisionListViewUserControl.ExpandAll 未迁移，跳过
        public void ExpandAllMerges()
        {
        }

        // 对照 WPF: public void ShowRevisionDetails(RevisionDiffTarget target, string fileToSelect = null)
        //   spike: RevisionDiffTarget（WPF-only）→ object 占位
        public void ShowRevisionDetails(object target, string fileToSelect = null)
        {
            _content?.RevisionDetails?.ShowRevisionDetails(target, fileToSelect);
        }

        // ===== EnsureLayoutInitialized（对照 WPF）=====

        // 对照 WPF: private void EnsureLayoutInitialized()
        //   首次调用时创建 Sidebar + Content + NotificationBar 装入对应容器，
        //   调用 Sidebar.Initialize(this) / Content.Initialize(this, Sidebar.SearchTabItem) /
        //   NotificationBar.Initialize(this)，恢复 SidebarColumnWidth，应用本地化。
        // spike: 改为 public（与现有 skeleton 兼容，调用方可显式触发初始化）
        public void EnsureLayoutInitialized()
        {
            if (_layoutInitialized)
            {
                return;
            }

            // 对照 WPF: Content = new RepositoryContentUserControl(); Sidebar = new SidebarUserControl();
            // spike: 用 DI 容器解析（保持 spike 装配链路）
            _sidebar = _serviceProvider.GetRequiredService<SidebarUserControl>();
            if (RepositorySidebarContainer != null)
            {
                RepositorySidebarContainer.Content = _sidebar;
            }

            _content = _serviceProvider.GetRequiredService<RepositoryContentUserControl>();
            if (RepositoryContentContainer != null)
            {
                RepositoryContentContainer.Content = _content;
            }

            // 对照 WPF: Sidebar.Initialize(this); Content.Initialize(this, Sidebar.SearchTabItem);
            // spike: Sidebar.SearchTabItem 是 Avalonia 自动生成的 internal 字段，外部不可访问；传 null
            _sidebar.Initialize(this);
            _content.Initialize(this, null);

            // 对照 WPF: Content.RevisionListViewUserControl.RevisionsDataSource.OnFetchRevisionsNeeded = FetchNextRevisionPage;
            // spike: RevisionsDataSource WPF-only，跳过

            // 对照 WPF: NotificationBar.Initialize(this);
            // spike: NotificationBar 用 DI 解析（已在 ServiceCollectionExtensions 注册）
            _notificationBar = _serviceProvider.GetRequiredService<NotificationBarUserControl>();
            if (NotificationBarContainer != null)
            {
                NotificationBarContainer.Content = _notificationBar;
            }
            _notificationBar.Initialize(this, null);

            // 对照 WPF: RestoreSidebarColumnWidth(); _layoutInitialized = true;
            //           Sidebar.RefreshTitle();
            //           PreferencesLocalization.Apply(Sidebar, ForkPlusSettings.Default.UiLanguage);
            //           PreferencesLocalization.Apply(Content, ForkPlusSettings.Default.UiLanguage);
            RestoreSidebarColumnWidth();
            _layoutInitialized = true;
            _sidebar.RefreshTitle();
            _sidebar.ApplyLocalization();
            _content.ApplyLocalization();
        }

        // ===== RepositoryStateChanged（对照 WPF）=====
        //   检查 RepositoryState 类型是否发生变化（用于决定是否清空 commit message）
        private static bool RepositoryStateChanged(RepositoryStatus oldStatus, RepositoryStatus newStatus)
        {
            RepositoryState oldState = oldStatus?.RepositoryState;
            if (oldState == null)
            {
                return false;
            }
            RepositoryState newState = newStatus?.RepositoryState;
            if (newState == null)
            {
                return false;
            }
            // 同类型状态不触发 commit message 重置
            if (oldState is RepositoryState.OK && newState is RepositoryState.OK) return false;
            if (oldState is RepositoryState.MergeInProgress && newState is RepositoryState.MergeInProgress) return false;
            if (oldState is RepositoryState.SquashInProgress && newState is RepositoryState.SquashInProgress) return false;
            if (oldState is RepositoryState.RebaseInProgress && newState is RepositoryState.RebaseInProgress) return false;
            if (oldState is RepositoryState.CherryPickInProgress && newState is RepositoryState.CherryPickInProgress) return false;
            if (oldState is RepositoryState.RevertInProgress && newState is RepositoryState.RevertInProgress) return false;
            if (oldState is RepositoryState.SequencerInProgress && newState is RepositoryState.SequencerInProgress) return false;
            if (oldState is RepositoryState.UnmergedIndex && newState is RepositoryState.UnmergedIndex) return false;
            if (oldState is RepositoryState.BisectInProgress && newState is RepositoryState.BisectInProgress) return false;
            if (oldState is RepositoryState.AmInProgress && newState is RepositoryState.AmInProgress) return false;
            return true;
        }

        // ===== SidebarColumnWidth 持久化（对照 WPF）=====

        // 对照 WPF: private void RestoreSidebarColumnWidth()
        private void RestoreSidebarColumnWidth()
        {
            double width = ForkPlusSettings.Default.SidebarColumnWidth;
            if (RepositoryUserControlGrid != null && RepositoryUserControlGrid.ColumnDefinitions.Count > 0)
            {
                RepositoryUserControlGrid.ColumnDefinitions[0].Width = new GridLength(width, GridUnitType.Pixel);
            }
        }

        // 对照 WPF: private void SaveSidebarColumnWidth()
        private void SaveSidebarColumnWidth()
        {
            if (RepositoryUserControlGrid == null || RepositoryUserControlGrid.ColumnDefinitions.Count == 0)
            {
                return;
            }
            double width = RepositoryUserControlGrid.ColumnDefinitions[0].Width.Value;
            ForkPlusSettings.Default.SidebarColumnWidth = width;
            ForkPlusSettings.Default.Save();
        }

        // ===== NotificationCenter 事件处理（对照 WPF）=====
        //   spike: NotificationCenter 不可访问，改为本地事件转发。

        // spike: 由调用方（MainWindow）转发 NotificationCenter.RepositoryNameChanged 事件
        public void RaiseRepositoryNameChanged(string repositoryPath)
        {
            if (GitModule == null) return;
            if (PathHelper.Normalize(GitModule.Path) == repositoryPath)
            {
                RefreshRepositoryName();
            }
            RepositoryNameChangedEvent?.Invoke(this, repositoryPath);
        }

        // spike: 由调用方（MainWindow）转发 NotificationCenter.RepositoryColorChanged 事件
        public void RaiseRepositoryColorChanged(string repositoryPath)
        {
            if (GitModule == null) return;
            if (PathHelper.Normalize(GitModule.Path) == repositoryPath)
            {
                RepositoryColorChangedEvent?.Invoke(this, repositoryPath);
                RepositoryUserControlColorChanged?.Invoke(this, this);
            }
        }

        // spike: 由调用方（MainWindow）转发 NotificationCenter.UpdateRepoStatusAutomaticallyChanged 事件
        public void RaiseUpdateRepoStatusAutomaticallyChanged(int interval)
        {
            UpdateRepoStatusAutomaticallyChangedEvent?.Invoke(this, interval);
            RepositoryUserControlIsDirtyChanged?.Invoke(this, this);
        }

        // 对照 WPF: private void RefreshRepositoryName()
        private void RefreshRepositoryName()
        {
            GitModule gitModule = GitModule;
            if (gitModule != null)
            {
                // 对照 WPF: RepositoryName = RepositoryManager.Instance.Repositories.FirstItemStruct(...)?.Name() ?? "unknown"
                // spike: FindRepositoryNameCallback 返回 null 时 fallback 到 gitModule.RepositoryName
                RepositoryName = FindRepositoryNameCallback != null
                    ? (FindRepositoryNameCallback(gitModule.Path) ?? gitModule.RepositoryName ?? "unknown")
                    : (gitModule.RepositoryName ?? "unknown");
                RefreshRepositoryTitle();
            }
            else
            {
                RepositoryName = null;
                RepositoryTitle = null;
            }
            RepositoryUserControlTitleChanged?.Invoke(this, this);
            _sidebar?.RefreshTitle();
        }

        // 对照 WPF: private static string FindSiblingWorktreeWithSameName(GitModule gitModule)
        private static string FindSiblingWorktreeWithSameName(GitModule gitModule)
        {
            string commonGitDir = gitModule.CommonGitDir;
            string repositoryName = gitModule.RepositoryName;
            string path = gitModule.Path;
            if (commonGitDir == null) return null;
            string path2 = Path.Combine(commonGitDir, "worktrees");
            if (!Directory.Exists(path2)) return null;
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(path2);
            }
            catch
            {
                return null;
            }
            foreach (string path3 in directories)
            {
                string text;
                try
                {
                    text = File.ReadAllText(Path.Combine(path3, "gitdir")).TrimEnd();
                }
                catch
                {
                    continue;
                }
                string text2;
                try
                {
                    text2 = Path.IsPathRooted(text)
                        ? Path.GetDirectoryName(text)
                        : Path.GetDirectoryName(Path.GetFullPath(Path.Combine(path3, text)));
                }
                catch
                {
                    continue;
                }
                if (text2 != null
                    && !string.Equals(text2, path, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFileName(text2), repositoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return text2;
                }
            }
            return null;
        }

        // ===== Invalidate / ResetSubdomains / InvalidateAndRefresh（对照 WPF）=====

        // 对照 WPF: public void Invalidate(SubDomain subdomains)
        public void Invalidate(SubDomain subdomains)
        {
            _invalidatedSubdomains |= subdomains;
        }

        // 对照 WPF: public void ResetSubdomains(SubDomain subdomains)
        public void ResetSubdomains(SubDomain subdomains)
        {
            _invalidatedSubdomains &= ~subdomains;
        }

        // 对照 WPF: public void InvalidateAndRefresh(SubDomain subdomains, RevisionSelector select, RepositoryViewMode priority)
        //   spike: RevisionSelector / RepositoryViewMode（WPF-only）→ object / string 占位
        public void InvalidateAndRefresh(SubDomain subdomains, object select = null, string priority = null)
        {
            EnsureLayoutInitialized();
            Invalidate(subdomains);
            CancelActiveFetchRevisionsJobs();

            // 对照 WPF: List<Sha> list = new List<Sha>();
            //   bottomShaInViewPort = Content.RevisionListViewUserControl.GetBottomShaInViewPort();
            //   bottomShaInSelection = Content.RevisionListViewUserControl.GetBottomShaInSelection();
            //   if (select is RevisionSelector.Sha sha) list.AddRange(sha.Shas);
            //   RefreshRepositoryCommand.Execute(this, list.ToArray(), select, priority);
            // spike: GetBottomShaInViewPort / GetBottomShaInSelection / RevisionSelector.Sha 等 WPF-only API 未迁移，
            //        通过注入的 RefreshRepositoryCallback 触发刷新链（调用方实现具体刷新逻辑）
            RefreshRepositoryCallback?.Invoke(this, subdomains, select, priority);
        }

        // 对照 WPF: public void InvalidateAndRefresh()  →  无参重载
        //   spike: priority 用 RevisionViewMode 字符串占位
        public void InvalidateAndRefresh()
        {
            InvalidateAndRefresh(SubDomain.All, null, RepositoryContentUserControl.RevisionViewMode);
        }

        // 对照 WPF: public void UncheckAmendCheckBox()
        public void UncheckAmendCheckBox()
        {
            if (_content?.CommitUserControl != null)
            {
                _content.CommitUserControl.AmendMode = false;
            }
        }

        // ===== SetRepositoryViewMode（spike 公共方法，对照 WPF ViewMode private setter 逻辑）=====

        // 对照 WPF: public RepositoryViewMode ViewMode { private set { ... } }
        //   spike: 公开为 SetRepositoryViewMode(string mode) 方法（spike skeleton 已有）
        public void SetRepositoryViewMode(string mode)
        {
            ViewMode = mode;
        }

        // ===== ShowLoading / HideLoading / DisableUserInterface / EnableUserInterface（spike 补充）=====
        //   对照 WPF 没有显式 ShowLoading/HideLoading 公共方法（在 spike 阶段补充为可见性切换辅助方法）

        public void ShowLoading()
        {
            if (_notificationBar != null)
            {
                _notificationBar.ShowNotification(new NotificationBarUserControl.NotificationViewModel
                {
                    Title = string.Empty,
                    Message = ServiceLocator.Localization != null
                        ? ServiceLocator.Localization.Current("Loading…")
                        : "Loading…",
                    Type = NotificationBarUserControl.NotificationType.Info,
                    AbortVisible = false,
                });
            }
            IsEnabled = false;
        }

        public void HideLoading()
        {
            _notificationBar?.Clear();
            IsEnabled = true;
        }

        public void DisableUserInterface()
        {
            IsEnabled = false;
        }

        public void EnableUserInterface()
        {
            IsEnabled = true;
        }

        // ===== v3.0.0+ Undo/Redo 子系统（对照 WPF ~390 行）=====

        // 对照 WPF: private UndoEntry TakeSnapshot(string operationName)
        //   v3.3.0：抓取当前仓库轻量 entry（HEAD sha + 当前分支名 + stash sha）
        private UndoEntry TakeSnapshot(string operationName)
        {
            try
            {
                GitCommandResult<UndoEntry> r = new SnapshotGitCommand().Execute(GitModule, operationName);
                return r.Succeeded ? r.Result : null;
            }
            catch
            {
                return null;
            }
        }

        // 对照 WPF: private bool IsWorkingTreeDirty()
        //   v3.3.0：实时检测工作区是否 dirty（取代旧 RepositorySnapshot.IsWorkingTreeDirty 字段）
        private bool IsWorkingTreeDirty()
        {
            try
            {
                GitRequestResult r = new GitRequest(GitModule).Command("status", "--porcelain").Execute(silent: true);
                return r.Success && !string.IsNullOrWhiteSpace(r.Stdout);
            }
            catch
            {
                return false;
            }
        }

        // 对照 WPF: public void RaiseUndoRedoStateChanged()
        public void RaiseUndoRedoStateChanged()
        {
            UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
        }

        // 对照 WPF: public Job AddUndoable(string operationName, Func<JobMonitor, GitCommandResult> action, JobFlags flags, bool showMessageWhenDone)
        //   v3.0.4：UndoRedoEnabled 开关。关闭时直接走 JobQueue.Add，跳过快照抓取（避免卡顿）。
        //   v3.3.0：操作成功后把 OperationName 写入 UndoIndexStore（持久化到 .git/forkplus-undo-index.json）
        public Job AddUndoable(string operationName, Func<JobMonitor, GitCommandResult> action,
            JobFlags flags = JobFlags.Default, bool showMessageWhenDone = true)
        {
            // v3.0.4：开关关闭时直接走原始 JobQueue.Add，不抓快照
            if (!ForkPlusSettings.Default.UndoRedoEnabled)
            {
                return JobQueue.Add(operationName, monitor => action(monitor), flags, showMessageWhenDone);
            }

            // 开关开启：在 Job 内抓 entry（后台线程，不阻塞 UI，可响应取消）
            return JobQueue.Add(operationName, monitor =>
            {
                // 1. Job 内抓 entry（后台线程执行，git 进程不阻塞 UI）
                UndoEntry entry = null;
                try
                {
                    entry = TakeSnapshot(operationName);
                }
                catch
                {
                    entry = null;
                }
                if (monitor.IsCanceled)
                {
                    return;
                }
                if (entry != null)
                {
                    UndoRedoStack.RecordBeforeOperation(entry);
                }

                // 2. 执行实际操作，失败时 CancelLastRecord
                GitCommandResult result = null;
                try
                {
                    result = action(monitor);
                }
                catch
                {
                    // v3.1.1：异常时也要看 IsCanceled。已取消的话栈顶 entry 应当弹出
                    UndoRedoStack.CancelLastRecord();
                    RaiseUndoRedoStateChanged();
                    throw;
                }
                // v3.1.1：用户在 action 执行过程中按了取消（IsCanceled=true）时，栈顶 entry 必须弹出
                if (monitor.IsCanceled || result == null || !result.Succeeded)
                {
                    UndoRedoStack.CancelLastRecord();
                    RaiseUndoRedoStateChanged();
                    return;
                }
                // v3.3.0：操作成功后，把 {entry.HeadSha → operationName} 写入持久化索引
                if (entry != null && !string.IsNullOrEmpty(entry.HeadSha))
                {
                    try
                    {
                        UndoIndexStore indexStore = new UndoIndexStore(GitModule);
                        indexStore.Record(new UndoIndexEntry(entry.HeadSha, operationName, entry.TimestampUtc));
                    }
                    catch
                    {
                        // 静默：索引写入失败不阻断主操作
                    }
                }
                RaiseUndoRedoStateChanged();
            }, flags, showMessageWhenDone);
        }

        // 对照 WPF: public Job AddUndoable(string operationName, Action<JobMonitor> action, JobFlags flags, bool showMessageWhenDone)
        //   调用方不返回 GitCommandResult 时使用此重载（优先使用 Func 重载）
        public Job AddUndoable(string operationName, Action<JobMonitor> action,
            JobFlags flags = JobFlags.Default, bool showMessageWhenDone = true)
        {
            return AddUndoable(operationName, monitor =>
            {
                action(monitor);
                return GitCommandResult.Success();
            }, flags, showMessageWhenDone);
        }

        // 对照 WPF: public void Undo()
        //   v3.3.0：恢复走 git reset --hard <target.HeadSha>（2 步：checkout + reset）
        public void Undo()
        {
            if (!UndoRedoStack.CanUndo)
            {
                return;
            }
            string opLabel = ServiceLocator.Localization != null
                ? ServiceLocator.Localization.Current("Undo")
                : "Undo";
            UndoEntry currentEntry = TakeSnapshot(opLabel);
            if (currentEntry == null)
            {
                RaiseUndoRedoStateChanged();
                return;
            }
            bool isDirty = IsWorkingTreeDirty();
            if (!ConfirmAndStashBeforeRestore(isDirty, opLabel, out bool shouldStashFirst))
            {
                return;
            }
            // P3.2：peek 目标 entry，检查 Undo 是否会回退已 push 的 commit
            UndoEntry peekedTarget = UndoRedoStack.UndoHistory.Count > 0 ? UndoRedoStack.UndoHistory[0] : null;
            bool forcePushAfterRestore = false;
            if (ShouldPromptForPushedCommits(currentEntry, peekedTarget))
            {
                if (!ConfirmPushedUndo(opLabel, out forcePushAfterRestore))
                {
                    return;
                }
            }
            UndoEntry target = UndoRedoStack.PopForUndo(currentEntry);
            if (target == null)
            {
                RaiseUndoRedoStateChanged();
                return;
            }
            JobQueue.Add(opLabel, monitor =>
            {
                GitCommandResult preResult = EnsureStashedIfNeeded(shouldStashFirst, opLabel, monitor);
                if (!preResult.Succeeded)
                {
                    ShowRestoreFailureAsync(preResult.Error);
                    return;
                }
                GitCommandResult result = new RestoreSnapshotGitCommand().Execute(GitModule, target, monitor);
                if (result.Succeeded && forcePushAfterRestore)
                {
                    // P3.2：本地恢复成功后，按用户选择执行 force push
                    GitCommandResult pushResult = ForcePushCurrentBranch(monitor);
                    Dispatcher.UIThread.Post(() =>
                    {
                        InvalidateAndRefresh(SubDomain.All);
                        RaiseUndoRedoStateChanged();
                        if (!pushResult.Succeeded)
                        {
                            ShowRestoreFailureAsync(pushResult.Error);
                        }
                    });
                    return;
                }
                Dispatcher.UIThread.Post(() =>
                {
                    InvalidateAndRefresh(SubDomain.All);
                    RaiseUndoRedoStateChanged();
                    if (!result.Succeeded)
                    {
                        ShowRestoreFailureAsync(result.Error);
                    }
                });
            });
        }

        // 对照 WPF: public void Redo()
        //   v3.3.0：恢复走 git reset --hard <target.HeadSha>（2 步：checkout + reset）
        public void Redo()
        {
            if (!UndoRedoStack.CanRedo)
            {
                return;
            }
            string opLabel = ServiceLocator.Localization != null
                ? ServiceLocator.Localization.Current("Redo")
                : "Redo";
            UndoEntry currentEntry = TakeSnapshot(opLabel);
            if (currentEntry == null)
            {
                RaiseUndoRedoStateChanged();
                return;
            }
            bool isDirty = IsWorkingTreeDirty();
            if (!ConfirmAndStashBeforeRestore(isDirty, opLabel, out bool shouldStashFirst))
            {
                return;
            }
            UndoEntry target = UndoRedoStack.PopForRedo(currentEntry);
            if (target == null)
            {
                RaiseUndoRedoStateChanged();
                return;
            }
            JobQueue.Add(opLabel, monitor =>
            {
                GitCommandResult preResult = EnsureStashedIfNeeded(shouldStashFirst, opLabel, monitor);
                if (!preResult.Succeeded)
                {
                    ShowRestoreFailureAsync(preResult.Error);
                    return;
                }
                GitCommandResult result = new RestoreSnapshotGitCommand().Execute(GitModule, target, monitor);
                Dispatcher.UIThread.Post(() =>
                {
                    InvalidateAndRefresh(SubDomain.All);
                    RaiseUndoRedoStateChanged();
                    if (!result.Succeeded)
                    {
                        ShowRestoreFailureAsync(result.Error);
                    }
                });
            });
        }

        // 对照 WPF: private bool ConfirmAndStashBeforeRestore(bool isDirty, string opLabel, out bool shouldStashFirst)
        //   P3.1：Undo/Redo 前 dirty 检查弹窗
        private bool ConfirmAndStashBeforeRestore(bool isDirty, string opLabel, out bool shouldStashFirst)
        {
            shouldStashFirst = false;
            if (!isDirty)
            {
                return true;
            }
            string message = ServiceLocator.Localization != null
                ? ServiceLocator.Localization.FormatCurrent(
                    "Working directory has uncommitted changes. {0} will discard them. Stash changes first?",
                    opLabel)
                : $"Working directory has uncommitted changes. {opLabel} will discard them. Stash changes first?";
            // 对照 WPF: MessageBox.Show(message, opLabel, MessageBoxButton.YesNo, MessageBoxImage.Question)
            // spike: ServiceLocator.Dialogs.ShowMessage(message, opLabel, YesNo, Question)
            DialogMessageBoxResult r = ServiceLocator.Dialogs.ShowMessage(
                message, opLabel, DialogMessageBoxButton.YesNo, DialogMessageBoxImage.Question);
            if (r != DialogMessageBoxResult.Yes)
            {
                return false;
            }
            shouldStashFirst = true;
            return true;
        }

        // 对照 WPF: private GitCommandResult EnsureStashedIfNeeded(bool shouldStashFirst, string opLabel, JobMonitor monitor)
        //   在 Job 内同步执行 stash（若需要）。失败时返回 Failure
        private GitCommandResult EnsureStashedIfNeeded(bool shouldStashFirst, string opLabel, JobMonitor monitor)
        {
            if (!shouldStashFirst)
            {
                return GitCommandResult.Success();
            }
            string stashMsg = ServiceLocator.Localization != null
                ? ServiceLocator.Localization.FormatCurrent("Auto-stash before {0}", opLabel)
                : $"Auto-stash before {opLabel}";
            GitCommandResult<bool> sr = new SaveStashGitCommand().Execute(GitModule, stashMsg, false, monitor);
            return sr.Succeeded ? GitCommandResult.Success() : GitCommandResult.Failure(sr.Error);
        }

        // 对照 WPF: private bool ShouldPromptForPushedCommits(UndoEntry currentEntry, UndoEntry target)
        //   P3.2：判断是否需要在 Undo 前提示"已 push"
        private bool ShouldPromptForPushedCommits(UndoEntry currentEntry, UndoEntry target)
        {
            if (currentEntry == null || target == null) return false;
            if (string.IsNullOrEmpty(currentEntry.HeadSha) || string.IsNullOrEmpty(target.HeadSha)) return false;
            if (currentEntry.HeadSha == target.HeadSha) return false;
            try
            {
                // 静默查询是否有 remote 分支包含当前 HEAD sha
                GitRequestResult r = new GitRequest(GitModule)
                    .Command("branch", "--list", "--remotes", "--contains", currentEntry.HeadSha)
                    .Execute(silent: true);
                if (!r.Success) return false;
                return !string.IsNullOrWhiteSpace(r.Stdout);
            }
            catch
            {
                return false;
            }
        }

        // 对照 WPF: private bool ConfirmPushedUndo(string opLabel, out bool forcePushAfterRestore)
        //   P3.2：弹窗询问用户如何处理已 push 的 commit
        private bool ConfirmPushedUndo(string opLabel, out bool forcePushAfterRestore)
        {
            forcePushAfterRestore = false;
            string message = ServiceLocator.Localization != null
                ? ServiceLocator.Localization.FormatCurrent(
                    "{0} will undo commit(s) that have been pushed to remote. Force push to remote too?",
                    opLabel)
                : $"{opLabel} will undo commit(s) that have been pushed to remote. Force push to remote too?";
            // 对照 WPF: MessageBox.Show(message, opLabel, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning)
            DialogMessageBoxResult r = ServiceLocator.Dialogs.ShowMessage(
                message, opLabel, DialogMessageBoxButton.YesNoCancel, DialogMessageBoxImage.Warning);
            if (r == DialogMessageBoxResult.Cancel)
            {
                return false;
            }
            forcePushAfterRestore = (r == DialogMessageBoxResult.Yes);
            return true;
        }

        // 对照 WPF: private GitCommandResult ForcePushCurrentBranch(JobMonitor monitor)
        //   P3.2：对当前分支执行 force push（--force-with-lease）
        private GitCommandResult ForcePushCurrentBranch(JobMonitor monitor)
        {
            try
            {
                // 对照 WPF: App.OverrideCredentialHelperBt + -c push.default=upstream push --force-with-lease
                // spike: ServiceLocator.GitEnvironment.OverrideCredentialHelperBt 取代 App.OverrideCredentialHelperBt
                GitCommand gitCommand = new GitCommand(
                    ServiceLocator.GitEnvironment.OverrideCredentialHelperBt,
                    "-c", "push.default=upstream", "push", "--force-with-lease", "--verbose", "--progress");
                GitRequestResult r = new GitRequest(GitModule).Command(gitCommand).Execute(monitor);
                if (!r.Success)
                {
                    return GitCommandResult.Failure(r.ToGitCommandError());
                }
                return GitCommandResult.Success();
            }
            catch (Exception ex)
            {
                return GitCommandResult.Failure(new GitCommandError.CallbackUnknownError(ex.Message));
            }
        }

        // 对照 WPF: private void ShowRestoreFailureAsync(GitCommandError error)
        //   Job 内失败时在 UI 线程弹错误窗并刷新 Undo/Redo 状态
        //   spike: ErrorWindow → ServiceLocator.Dialogs.ShowError；Dispatcher.Async → Dispatcher.UIThread.Post
        private void ShowRestoreFailureAsync(GitCommandError error)
        {
            Dispatcher.UIThread.Post(() =>
            {
                string title = ServiceLocator.Localization != null
                    ? ServiceLocator.Localization.Current("Restore failed")
                    : "Restore failed";
                string message = error != null
                    ? (error.FriendlyDescription ?? error.ToString() ?? "Unknown error")
                    : "Unknown error";
                ServiceLocator.Dialogs?.ShowError(title, message);
                RaiseUndoRedoStateChanged();
            });
        }
    }
}
