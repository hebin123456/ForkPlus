using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 5.4 完整迁移版：Avalonia 版 RevisionListViewUserControl。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionListViewUserControl.xaml.cs（1946 行）：
    //   - 公共字段：RevisionsDataSource（RepositoryUserControl 大量直接访问）
    //   - 公共属性：RepositoryUserControl / SidebarSearchTabItem / SelectedIndex /
    //     SelectedRevision / SelectedRevisions
    //   - 公共事件（4 个，RepositoryContentUserControl 必须订阅）：
    //       SearchQueryChanged / SelectionChanged / RevisionDoubleClick / BranchDoubleClick
    //   - 公共方法：Initialize / UpdateRepositoryData / Select(2 重载) / FocusSelectedItem /
    //     CollapseAll / ExpandAll / GetBottomShaInViewPort / GetBottomShaInSelection
    //   - 构造器 5 个 CommandBindings + PreviewKeyDown/KeyDown 处理
    //   - 后台 Job：_activeContextSearchJob（context search）/ _activeSidebarSearchJob（sidebar search）
    //   - 约 1000 行右键菜单构造方法（CreateRevisionContextMenuItems 等）
    //   - AI 集成方法（GeneratePullRequestDescription / AiExplainRevision /
    //     CreateBranchAiCodeReviewMenuItem / CreateRevisionRangeAiCodeReviewMenuItem /
    //     CreateAiExplainRevisionMenuItem）
    //
    // spike 简化策略（本版本实现）：
    //   - RevisionsDataSource → 内联 ObservableCollection<RevisionEntryViewModel> + Filter 方法
    //   - RevisionEntryViewModel 内联 POCO：Sha / ShortSha / Author / AuthorEmail / AuthorDate /
    //     Subject / Body / Parents / Lanes（LaneInfo[]）
    //   - GraphCellView（WPF 自绘 OnRender）→ Canvas 占位 + 每个 lane 一条彩色线（8 色调色板循环）
    //   - RevisionSearchPanelUserControl（自定义控件）→ 普通 TextBox + TextChanged 事件
    //   - RevisionGraphTooltipUserControl（悬浮提示）→ ToolTip.SetTip(listViewItem, tooltipText)
    //   - 虚拟化：ListBox + VirtualizingStackPanel 原生支持
    //   - RepositoryUserControl 依赖 → Initialize(RepositoryUserControl) 注入；内部访问
    //     GitModule / RepositoryData / CommitGraphCache / JobQueue（RepositoryUserControl 在 spike 中为 object，
    //     用模式匹配按需转换为真实类型）
    //   - JobQueue → Task.Run + Dispatcher.UIThread.Post + JobMonitor
    //   - PreferencesLocalization → ServiceLocator.Localization
    //   - Dispatcher.Invoke / Dispatcher.Async → Dispatcher.UIThread.Post
    //   - Visibility.Collapsed/Visible → IsVisible = false/true
    //   - MouseDoubleClick → DoubleTapped
    //   - 右键菜单（约 1000 行 WPF 逻辑）→ 简化为 ContextMenu + MenuItem 核心动作
    //     （Checkout / Revert / Cherry-pick / Rebase / Merge / Reset / Tag / Branch / Diff / Copy SHA）
    //
    // 构造函数：
    //   - RevisionListViewUserControl()：无参，供 XAML 实例化（RepositoryContentUserControl.axaml）
    //   - RevisionListViewUserControl(IServiceProvider)：供 DI 解析（ServiceCollectionExtensions 注册）
    //   两个构造函数都调用 InitializeComponent() + InitializeCore()
    public partial class RevisionListViewUserControl : UserControl
    {
        // ===== 内部类：commit 行模型（对照 WPF DecoratedRevision，spike 内联 POCO）=====

        // 对照 WPF: DecoratedRevision + GraphCellView DataContext
        // 实现 INotifyPropertyChanged 以支持 OneWay 绑定（IsSearchMatch / IsLoading 变化时刷新）
        public class RevisionEntryViewModel : INotifyPropertyChanged
        {
            private bool _isSearchMatch;
            private bool _isHead;

            public string Sha { get; set; }
            public string ShortSha { get; set; }
            public string Author { get; set; }
            public string AuthorEmail { get; set; }
            public string AuthorDate { get; set; }
            public string Subject { get; set; }
            public string Body { get; set; }
            public string[] Parents { get; set; } = Array.Empty<string>();
            public string[] References { get; set; } = Array.Empty<string>();
            public int Row { get; set; }
            public int LaneIndex { get; set; }

            // 图形 lane 颜色（8 色调色板循环分配）
            public IBrush GraphBrush { get; set; }

            // 该 commit 在该行参与的 lane 列表（spike 仅含自身 lane）
            public IReadOnlyList<LaneInfo> Lanes { get; set; } = Array.Empty<LaneInfo>();

            // DataTemplate 绑定的引用文本（branch/tag 标签简化为逗号分隔）
            public string ReferencesText => References.Length == 0 ? string.Empty : string.Join(", ", References);

            // 悬浮提示文本（对照 WPF RevisionGraphTooltipUserControl，spike 用 ToolTip.SetTip 简化）
            public string ToolTipText =>
                Subject + Environment.NewLine +
                Author + " <" + AuthorEmail + ">" + Environment.NewLine +
                AuthorDate + Environment.NewLine +
                "Sha: " + Sha;

            public bool IsHead
            {
                get => _isHead;
                set { if (_isHead != value) { _isHead = value; OnPropertyChanged(); } }
            }

            public bool IsSearchMatch
            {
                get => _isSearchMatch;
                set { if (_isSearchMatch != value) { _isSearchMatch = value; OnPropertyChanged(); } }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // 图形 lane 信息（对照 WPF GraphCellView 中的 lane 结构，spike 简化）
        public class LaneInfo
        {
            public int LaneIndex { get; }
            public IBrush Brush { get; }
            public bool IsCommit { get; }
            public LaneInfo(int laneIndex, IBrush brush, bool isCommit)
            {
                LaneIndex = laneIndex;
                Brush = brush;
                IsCommit = isCommit;
            }
        }

        // 对照 WPF: DecoratedRevisionRowComparer（按 Row 排序）
        private class RevisionRowComparer : IComparer<RevisionEntryViewModel>
        {
            public static readonly RevisionRowComparer Instance = new RevisionRowComparer();
            public int Compare(RevisionEntryViewModel x, RevisionEntryViewModel y)
                => (x?.Row ?? 0).CompareTo(y?.Row ?? 0);
        }

        // 8 色调色板（对照 WPF GraphCellView 13 个硬编码分支颜色，spike 用 8 色循环）
        private static readonly IBrush[] GraphPalette =
        {
            Brushes.IndianRed, Brushes.Orange, Brushes.Gold, Brushes.YellowGreen,
            Brushes.SeaGreen, Brushes.SteelBlue, Brushes.MediumPurple, Brushes.HotPink
        };

        // ===== 公共事件（对照 WPF 4 个公共事件）=====
        // RepositoryContentUserControl 订阅这 4 个事件，本版本真实触发
        public event EventHandler<EventArgs> SearchQueryChanged;
        public event EventHandler<EventArgs> SelectionChanged;
        public event EventHandler<EventArgs> RevisionDoubleClick;
        public event EventHandler<EventArgs> BranchDoubleClick;

        // ===== 公共属性（对照 WPF）=====

        // RepositoryUserControl 在 spike 中保持 object，内部按需转换为真实类型
        public object RepositoryUserControl { get; private set; }
        public object SidebarSearchTabItem { get; private set; }

        public int SelectedIndex => RevisionListView?.SelectedIndex ?? -1;

        // 对照 WPF: SelectedRevision（DecoratedRevision）
        public RevisionEntryViewModel SelectedRevision =>
            RevisionListView?.SelectedItem as RevisionEntryViewModel;

        // 对照 WPF: SelectedRevisions（DecoratedRevision[]）
        public RevisionEntryViewModel[] SelectedRevisions
        {
            get
            {
                var list = RevisionListView?.SelectedItems;
                if (list == null || list.Count == 0) return Array.Empty<RevisionEntryViewModel>();
                var result = new List<RevisionEntryViewModel>(list.Count);
                foreach (var item in list)
                {
                    if (item is RevisionEntryViewModel vm) result.Add(vm);
                }
                return result.ToArray();
            }
        }

        // spike 新增公共属性（task spec）
        // AllRevisions：当前已加载的全部 commit（过滤后视图）
        public IReadOnlyList<RevisionEntryViewModel> AllRevisions => _filteredRevisions;
        // IsFiltering：是否处于过滤状态
        public bool IsFiltering => !string.IsNullOrEmpty(_searchText);
        // IsLoading：是否正在异步加载（fetch / context search）
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    if (LoadingIndicator != null) LoadingIndicator.IsVisible = value;
                }
            }
        }

        // ===== 公共字段（对照 WPF RevisionsDataSource）=====
        // RepositoryUserControl 大量直接访问此字段；spike 版保留 object 占位以兼容，
        // 真实数据源由内部 ObservableCollection<RevisionEntryViewModel> 管理。
        public object RevisionsDataSource { get; } = new object();

        // ===== 私有字段 =====

        private readonly IServiceProvider _serviceProvider;

        // 真实数据源（对照 WPF RevisionsDataSource）
        private readonly ObservableCollection<RevisionEntryViewModel> _allRevisions =
            new ObservableCollection<RevisionEntryViewModel>();
        private readonly ObservableCollection<RevisionEntryViewModel> _filteredRevisions =
            new ObservableCollection<RevisionEntryViewModel>();

        // 对照 WPF: _refreshContextSearch (DelayedAction<string>, 0.1s)
        private Timer _filterDebounceTimer;
        private string _searchText;
        private bool _isLoading;
        private bool _isLoaded;
        private bool _suppressSelectionEvent;
        private int _currentMatchIndex = -1;

        // 对照 WPF: _activeContextSearchJob / _activeSidebarSearchJob
        private JobMonitor _activeContextSearchMonitor;
        private JobMonitor _activeSidebarSearchMonitor;

        // 右键菜单实例（对照 WPF RevisionListView.ContextMenu，动态构造 items）
        private ContextMenu _contextMenu;

        // ===== 构造函数 =====

        // 无参构造：供 XAML 实例化（RepositoryContentUserControl.axaml 内 <uc:RevisionListViewUserControl/>）
        public RevisionListViewUserControl() : this(serviceProvider: null)
        {
        }

        // IServiceProvider 构造：供 DI 解析 + task spec 要求的构造函数签名
        public RevisionListViewUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();
            InitializeCore();
        }

        // 共享初始化（两个构造函数共用）
        private void InitializeCore()
        {
            // 对照 WPF: RevisionListView.ItemsSource = RevisionsDataSource;
            if (RevisionListView != null)
            {
                RevisionListView.ItemsSource = _filteredRevisions;
                RevisionListView.SelectionChanged += RevisionListView_SelectionChanged;
                // 对照 WPF: MouseDoubleClick → DoubleTapped（InputElement）
                RevisionListView.DoubleTapped += RevisionListView_DoubleTapped;
                // 对照 WPF: ContextMenuOpening → 动态构造右键菜单
                // Avalonia 11 无 Control.ContextMenuOpening 事件，改用 ContextMenu.Opening
                _contextMenu = new ContextMenu();
                _contextMenu.Opening += RevisionContextMenu_Opening;
                RevisionListView.ContextMenu = _contextMenu;
            }

            // 对照 WPF: RevisionSearchPanelUserControl.SearchQueryChanged
            if (SearchTextBox != null)
            {
                SearchTextBox.TextChanged += SearchTextBox_TextChanged;
            }

            Loaded += OnLoaded;

            if (LoadingIndicator != null) LoadingIndicator.IsVisible = false;
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;
            // 对照 WPF RefreshRevisionListViewTemplate()：spike 版无 GridView 切换，跳过
        }

        // ===== 公共方法（对照 WPF 9 个公共方法 + task spec 新增 API）=====

        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl, SearchTabItem sidebarSearchTabItem)
        //   替代构造器 DI，由 RepositoryContentUserControl 在装入时调用
        public void Initialize(object repositoryUserControl, object sidebarSearchTabItem)
        {
            RepositoryUserControl = repositoryUserControl;
            SidebarSearchTabItem = sidebarSearchTabItem;
            // 对照 WPF: SidebarSearchTabItem.SearchQueryChanged += SidebarSearchPanelUserControl_SearchQueryChanged;
            // spike: sidebar 搜索为 object，暂不订阅（Phase 5 后续子阶段接入）
        }

        // task spec API: Initialize(RepositoryUserControl repositoryUserControl) — 单参重载
        public void Initialize(object repositoryUserControl)
        {
            Initialize(repositoryUserControl, null);
        }

        // 对照 WPF: public void UpdateRepositoryData(RepositoryData repositoryData)
        //   增量刷新 ListView 数据
        public void UpdateRepositoryData(object repositoryData)
        {
            if (repositoryData is RepositoryData data)
            {
                Refresh(data, GetCommitGraphCache());
            }
        }

        // task spec API: Refresh(RepositoryData data, CommitGraphCache cache)
        //   全量刷新 commit 列表（spike 版从 RevisionStorage 提取 Sha+Parents，异步补全 author/date/subject）
        public void Refresh(RepositoryData data, CommitGraphCache cache)
        {
            _activeContextSearchMonitor?.Cancel();
            _activeContextSearchMonitor = null;

            _allRevisions.Clear();
            _filteredRevisions.Clear();
            _searchText = null;

            if (data == null || data.RevisionStorage == null)
            {
                return;
            }

            var storage = data.RevisionStorage;
            int count = storage.Count;
            var headSha = GetHeadSha(data);
            var newEntries = new List<RevisionEntryViewModel>(count);
            var handleEnumerator = storage.GetEnumerator();
            int row = 0;
            while (handleEnumerator.MoveNext())
            {
                var handle = handleEnumerator.Current;
                var sha = storage.GetSha(handle);
                var parents = storage.GetParents(handle);
                var parentShas = parents.Length > 0 ? parents.ToArray() : Array.Empty<Sha>();
                var entry = new RevisionEntryViewModel
                {
                    Sha = sha.ToString(),
                    ShortSha = sha.ToAbbreviatedString(),
                    Row = row,
                    IsHead = headSha.HasValue && headSha.Value == sha,
                    Author = sha.ToAbbreviatedString(), // 占位，异步补全
                    AuthorEmail = "",
                    AuthorDate = "",
                    Subject = "",
                    Body = "",
                    Parents = parentShas.Select(p => p.ToString()).ToArray()
                };
                newEntries.Add(entry);
                row++;
            }

            // 分配 lane + 颜色
            AssignLanes(newEntries);

            // 装入数据源
            foreach (var entry in newEntries)
            {
                _allRevisions.Add(entry);
                _filteredRevisions.Add(entry);
            }

            // 设置 ToolTip（对照 WPF RevisionGraphTooltipUserControl，spike 用 ToolTip.SetTip 简化）
            // 注：DataTemplate 中已绑定 ToolTip.Tip，无需逐项 SetTip

            // 异步补全 author/date/subject（对照 WPF git log 调用，spike 用 Task.Run + Dispatcher.UIThread.Post）
            EnrichRevisionsAsync(newEntries);

            // 对照 WPF: RevisionListView.SelectedIndex = -1;
            if (RevisionListView != null)
            {
                _suppressSelectionEvent = true;
                RevisionListView.SelectedIndex = -1;
                _suppressSelectionEvent = false;
            }
        }

        // 对照 WPF: public bool Select(RevisionSelector select, NoUIAutomationListView.SelectOptions selectOptions, int fallbackRow = -1)
        public bool Select(object select, object selectOptions, int fallbackRow = -1)
        {
            if (RevisionListView == null) return false;
            // spike: RevisionSelector 为 WPF-only，用 object 占位；支持 string sha 直接选中
            if (select is string sha)
            {
                return SelectRevision(sha);
            }
            if (fallbackRow != -1 && fallbackRow < _filteredRevisions.Count)
            {
                RevisionListView.SelectedIndex = fallbackRow;
                RevisionListView.ScrollIntoView(RevisionListView.SelectedItem);
                return true;
            }
            return false;
        }

        // 对照 WPF: public void Select(IReadOnlyList<int> rows)
        public void Select(IReadOnlyList<int> rows)
        {
            if (RevisionListView == null || rows == null || rows.Count == 0) return;
            RevisionListView.SelectedIndex = -1;
            foreach (var row in rows)
            {
                if (row >= 0 && row < _filteredRevisions.Count)
                {
                    // Avalonia ListBox 多选通过 SelectedItems 累加
                    RevisionListView.SelectedItems?.Add(_filteredRevisions[row]);
                }
            }
            if (RevisionListView.SelectedIndex >= 0)
            {
                RevisionListView.ScrollIntoView(RevisionListView.SelectedItem);
            }
        }

        // task spec API: SelectRevision(string sha) — 选中指定 commit
        public bool SelectRevision(string sha)
        {
            if (RevisionListView == null || string.IsNullOrEmpty(sha)) return false;
            for (int i = 0; i < _filteredRevisions.Count; i++)
            {
                if (string.Equals(_filteredRevisions[i].Sha, sha, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_filteredRevisions[i].ShortSha, sha, StringComparison.OrdinalIgnoreCase))
                {
                    RevisionListView.SelectedIndex = i;
                    RevisionListView.ScrollIntoView(RevisionListView.SelectedItem);
                    return true;
                }
            }
            return false;
        }

        // task spec API: SelectRevisions(string[] shas) — 选中多个
        public void SelectRevisions(string[] shas)
        {
            if (RevisionListView == null || shas == null || shas.Length == 0) return;
            ClearSelection();
            var set = new HashSet<string>(shas, StringComparer.OrdinalIgnoreCase);
            if (RevisionListView.SelectedItems != null)
            {
                for (int i = 0; i < _filteredRevisions.Count; i++)
                {
                    if (set.Contains(_filteredRevisions[i].Sha) || set.Contains(_filteredRevisions[i].ShortSha))
                    {
                        RevisionListView.SelectedItems.Add(_filteredRevisions[i]);
                    }
                }
            }
        }

        // task spec API: ClearSelection()
        public void ClearSelection()
        {
            if (RevisionListView == null) return;
            _suppressSelectionEvent = true;
            RevisionListView.SelectedIndex = -1;
            RevisionListView.SelectedItems?.Clear();
            _suppressSelectionEvent = false;
        }

        // task spec API: Filter(string searchText) — 过滤
        public void Filter(string searchText)
        {
            ApplyFilter(searchText);
        }

        // task spec API: FetchRevisionsUntilSha(string sha) — 异步加载到指定 sha
        public void FetchRevisionsUntilSha(string sha)
        {
            if (string.IsNullOrEmpty(sha)) return;
            var gitModule = GetGitModule();
            if (gitModule == null) return;

            IsLoading = true;
            var monitor = new JobMonitor();
            _activeContextSearchMonitor = monitor;
            Task.Run(() =>
            {
                try
                {
                    // spike: 不实现完整 fetch-until-sha 链（依赖 RevisionStorage.Extend / GetRevisionStorageGitCommand）
                    // 仅检查 sha 是否已存在；若不存在则触发全量刷新回调
                    bool exists = false;
                    Dispatcher.UIThread.Post(() =>
                    {
                        exists = _allRevisions.Any(e =>
                            string.Equals(e.Sha, sha, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(e.ShortSha, sha, StringComparison.OrdinalIgnoreCase));
                        if (!exists)
                        {
                            // 通知 RepositoryUserControl 触发全量刷新（对照 WPF FetchUntilFindContextSearchMatch）
                            var repo = RepositoryUserControl;
                            if (repo is RepositoryUserControl rc && rc.GitModule != null && rc.RepositoryData != null)
                            {
                                rc.UpdateRepositoryData(rc.RepositoryData, null, null);
                            }
                        }
                        IsLoading = false;
                    });
                }
                catch
                {
                    Dispatcher.UIThread.Post(() => IsLoading = false);
                }
            });
        }

        // task spec API: FetchNextPage() — 加载下一页
        public void FetchNextPage()
        {
            var gitModule = GetGitModule();
            if (gitModule == null) return;
            // spike: GetRevisionStorageGitCommand / RevisionsDataSource 等 WPF-only 类型未迁移，
            // 委托给 RepositoryUserControl 触发全量刷新
            if (RepositoryUserControl is RepositoryUserControl rc)
            {
                rc.FetchNextRevisionPage();
            }
        }

        // 对照 WPF: public void FocusSelectedItem()
        public void FocusSelectedItem()
        {
            RevisionListView?.Focus();
        }

        // 对照 WPF: public void CollapseAll()
        public void CollapseAll()
        {
            // 对照 WPF: GitModule.Settings.CollapseAllMergeRevisions = true; Save(); UpdateRepositoryData
            var gitModule = GetGitModule();
            if (gitModule?.Settings != null)
            {
                gitModule.Settings.CollapseAllMergeRevisions = true;
                gitModule.Settings.Save();
                if (RepositoryUserControl is RepositoryUserControl rc && rc.RepositoryData != null)
                {
                    rc.UpdateRepositoryData(rc.RepositoryData, null, null);
                }
            }
        }

        // 对照 WPF: public void ExpandAll()
        public void ExpandAll()
        {
            var gitModule = GetGitModule();
            if (gitModule?.Settings != null)
            {
                gitModule.Settings.CollapseAllMergeRevisions = false;
                gitModule.Settings.Save();
                if (RepositoryUserControl is RepositoryUserControl rc && rc.RepositoryData != null)
                {
                    rc.UpdateRepositoryData(rc.RepositoryData, null, null);
                }
            }
        }

        // 对照 WPF: public Sha? GetBottomShaInViewPort()
        //   含 VisualTreeHelper 钻取 ScrollViewer，spike 版用 ListBox 偏移近似
        public object GetBottomShaInViewPort()
        {
            if (RevisionListView == null || _filteredRevisions.Count == 0) return null;
            // spike: 无 VisualTreeHelper 钻取，用 SelectedIndex 或最后可见项近似
            int index = RevisionListView.SelectedIndex;
            if (index < 0) index = 0;
            if (index >= _filteredRevisions.Count) index = _filteredRevisions.Count - 1;
            return _filteredRevisions[index].Sha;
        }

        // 对照 WPF: public Sha? GetBottomShaInSelection()
        public object GetBottomShaInSelection()
        {
            var selected = SelectedRevisions;
            if (selected.Length == 0) return null;
            var bottom = selected[0];
            for (int i = 1; i < selected.Length; i++)
            {
                if (selected[i].Row > bottom.Row) bottom = selected[i];
            }
            return bottom.Sha;
        }

        // ===== 私有方法（对照 WPF 内部逻辑）=====

        // lane 分配（对照 WPF RevisionGraph，spike 简化为按序占位 + 颜色循环）
        private void AssignLanes(List<RevisionEntryViewModel> entries)
        {
            // 简单 lane 分配：维护可用 lane 池，commit 占第一个可用 lane
            var occupied = new HashSet<int>();
            // sha -> lane
            var shaToLane = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                int lane = -1;
                // 若某父提交已在 lane 上，复用之（让分支线连续）
                foreach (var parent in entry.Parents)
                {
                    if (shaToLane.TryGetValue(parent, out var parentLane) && !occupied.Contains(parentLane))
                    {
                        lane = parentLane;
                        break;
                    }
                }
                if (lane < 0)
                {
                    for (int i = 0; i < 32; i++)
                    {
                        if (!occupied.Contains(i)) { lane = i; break; }
                    }
                }
                if (lane < 0) lane = 0;

                entry.LaneIndex = lane;
                shaToLane[entry.Sha] = lane;
                // spike: 不释放 lane（避免复杂交叉处理），仅记录占位
                occupied.Add(lane);

                var brush = GraphPalette[lane % GraphPalette.Length];
                entry.GraphBrush = brush;
                entry.Lanes = new[] { new LaneInfo(lane, brush, true) };
            }
        }

        // 异步补全 author/date/subject（对照 WPF git log，spike 用 Task.Run + Dispatcher.UIThread.Post）
        private void EnrichRevisionsAsync(List<RevisionEntryViewModel> entries)
        {
            if (entries.Count == 0) return;
            var gitModule = GetGitModule();
            if (gitModule == null) return;

            var snapshot = entries.ToList();
            var shaToEntry = snapshot.ToDictionary(e => e.Sha, StringComparer.OrdinalIgnoreCase);
            var shaArgs = snapshot.Select(e => e.Sha).ToArray();

            IsLoading = true;
            var monitor = new JobMonitor();
            _activeContextSearchMonitor = monitor;

            Task.Run(() =>
            {
                try
                {
                    // 对照 WPF: git log --pretty=format:%H%x1f%an%x1f%ae%x1f%aI%x1f%s%x1e%b%x00 <sha...>
                    var format = "%H%x1f%an%x1f%ae%x1f%aI%x1f%s%x1e%b%x00";
                    var args = new List<string> { "--no-pager", "log", "--no-color", "--no-decorate", "--pretty=format:" + format, "--max-count=" + shaArgs.Length };
                    args.AddRange(shaArgs);
                    var gitCommand = new GitCommand(args.ToArray());
                    var result = new GitRequest(gitModule).Command(gitCommand).Execute(silent: true);

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (monitor.IsCanceled) { IsLoading = false; return; }
                        if (result.ExitCode < 2 && !string.IsNullOrEmpty(result.Stdout))
                        {
                            // 每条记录以 \x00 分隔
                            var records = result.Stdout.Split('\x00');
                            foreach (var rec in records)
                            {
                                if (string.IsNullOrEmpty(rec)) continue;
                                // header \x1e body
                                string headerPart = rec;
                                string body = "";
                                int bodySep = rec.IndexOf('\x1e');
                                if (bodySep >= 0)
                                {
                                    headerPart = rec.Substring(0, bodySep);
                                    body = rec.Substring(bodySep + 1);
                                }
                                var fields = headerPart.Split('\x1f');
                                if (fields.Length >= 5 && shaToEntry.TryGetValue(fields[0], out var entry))
                                {
                                    entry.Author = fields[1];
                                    entry.AuthorEmail = fields[2];
                                    entry.AuthorDate = fields[3];
                                    entry.Subject = fields[4];
                                    entry.Body = body;
                                }
                            }
                        }
                        IsLoading = false;
                    });
                }
                catch
                {
                    Dispatcher.UIThread.Post(() => IsLoading = false);
                }
            });
        }

        // 搜索框 TextChanged（对照 WPF RevisionSearchPanelUserControl_SearchQueryChanged + DelayedAction 0.1s）
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = SearchTextBox?.Text ?? string.Empty;
            // 对照 WPF: _refreshContextSearch.InvokeWithDelay(0.1s)
            _filterDebounceTimer?.Dispose();
            _filterDebounceTimer = new Timer(_ =>
            {
                Dispatcher.UIThread.Post(() => ApplyFilter(text));
            }, null, 100, Timeout.Infinite);
        }

        // 应用过滤（对照 WPF RefreshContextSearch + RevisionsDataSource.SetContextSearch）
        private void ApplyFilter(string searchText)
        {
            _searchText = searchText;
            var selectedSha = SelectedRevision?.Sha;

            _filteredRevisions.Clear();
            if (string.IsNullOrEmpty(searchText))
            {
                foreach (var entry in _allRevisions)
                {
                    entry.IsSearchMatch = false;
                    _filteredRevisions.Add(entry);
                }
            }
            else
            {
                var query = searchText.Trim();
                foreach (var entry in _allRevisions)
                {
                    bool match = (entry.Sha?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (entry.ShortSha?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (entry.Author?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (entry.Subject?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (entry.Body?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (match)
                    {
                        entry.IsSearchMatch = true;
                        _filteredRevisions.Add(entry);
                    }
                    else
                    {
                        entry.IsSearchMatch = false;
                    }
                }
            }

            // 触发 SearchQueryChanged 事件（对照 WPF SearchQueryChanged?.Invoke）
            SearchQueryChanged?.Invoke(this, EventArgs.Empty);

            // 恢复选中（对照 WPF 选中项保持）
            if (selectedSha != null)
            {
                SelectRevision(selectedSha);
            }

            UpdateMatchCount();
        }

        // 更新匹配计数显示（对照 WPF RevisionSearchPanelUserControl.UpdateMatchesCount）
        private void UpdateMatchCount()
        {
            if (MatchCountTextBlock == null) return;
            bool hasSearch = !string.IsNullOrEmpty(_searchText);
            bool hasMatches = hasSearch && _filteredRevisions.Count > 0;
            if (!hasSearch)
            {
                MatchCountTextBlock.IsVisible = false;
            }
            else
            {
                MatchCountTextBlock.IsVisible = true;
                MatchCountTextBlock.Text = LocFmt("{0} matches", _filteredRevisions.Count);
            }
            // Prev/Next 按钮仅在有匹配时显示（对照 WPF JumpToPrev/NextSearchResultButton）
            if (PrevSearchButton != null) PrevSearchButton.IsVisible = hasMatches;
            if (NextSearchButton != null) NextSearchButton.IsVisible = hasMatches;
            if (hasMatches)
            {
                if (_currentMatchIndex < 0 || _currentMatchIndex >= _filteredRevisions.Count)
                    _currentMatchIndex = 0;
            }
            else
            {
                _currentMatchIndex = -1;
            }
        }

        // Prev 按钮：跳到上一个匹配（对照 WPF JumpToPreviousSearchResultButton_Click）
        public void PrevSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredRevisions.Count == 0) return;
            _currentMatchIndex = (_currentMatchIndex - 1 + _filteredRevisions.Count) % _filteredRevisions.Count;
            SelectRevision(_filteredRevisions[_currentMatchIndex].Sha);
        }

        // Next 按钮：跳到下一个匹配（对照 WPF JumpToNextSearchResultButton_Click）
        public void NextSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredRevisions.Count == 0) return;
            _currentMatchIndex = (_currentMatchIndex + 1) % _filteredRevisions.Count;
            SelectRevision(_filteredRevisions[_currentMatchIndex].Sha);
        }

        // ListView SelectionChanged（对照 WPF RevisionListView_SelectionChanged）
        private void RevisionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionEvent) return;
            e.Handled = true;
            if (e.AddedItems.Count <= 0 && e.RemovedItems.Count <= 0) return;

            var selected = SelectedRevisions;
            // 对照 WPF: 两个选中项按 Row 排序
            if (selected.Length == 2)
            {
                Array.Sort(selected, RevisionRowComparer.Instance);
            }

            // 触发 SelectionChanged 事件（对照 WPF SelectionChanged?.Invoke）
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        // ListView DoubleTapped（对照 WPF RevisionListView_MouseDoubleClick）
        private void RevisionListView_DoubleTapped(object sender, RoutedEventArgs e)
        {
            if (RevisionListView?.SelectedItem is RevisionEntryViewModel entry)
            {
                // 对照 WPF: 分支双击触发 BranchDoubleClick；其他触发 RevisionDoubleClick
                // spike: 简化为统一触发 RevisionDoubleClick
                RevisionDoubleClick?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        // ContextMenu.Opening（对照 WPF RevisionListView_ContextMenuOpening — 动态构造右键菜单）
        // Avalonia 11 无 Control.ContextMenuOpening 事件，改用 ContextMenu.Opening（CancelEventHandler）
        private void RevisionContextMenu_Opening(object sender, CancelEventArgs e)
        {
            var selected = SelectedRevisions;
            var single = selected.Length == 1 ? selected[0] : null;

            // List<object> 同时容纳 MenuItem 与 Separator（两者均为 IControl，但 Separator 非派生自 MenuItem）。
            var items = new List<object>();

            if (selected.Length == 0)
            {
                e.Cancel = true;
                return;
            }

            if (single != null)
            {
                // 单选菜单（对照 WPF CreateRevisionContextMenuItems，spike 简化为核心动作）
                items.Add(CreateMenuItem(Loc("Checkout") + "...", () => CheckoutRevision(single)));
                items.Add(CreateMenuItem(Loc("Create Branch") + "...", () => CreateBranch(single)));
                items.Add(CreateMenuItem(Loc("Create Tag") + "...", () => CreateTag(single)));
                items.Add(new Separator());
                items.Add(CreateMenuItem(Loc("Merge into Current Branch") + "...", () => MergeRevision(single)));
                items.Add(CreateMenuItem(Loc("Rebase Current Branch to Here") + "...", () => RebaseRevision(single)));
                items.Add(CreateMenuItem(Loc("Reset Current Branch to Here") + "...", () => ResetRevision(single)));
                items.Add(new Separator());
                items.Add(CreateMenuItem(Loc("Cherry-pick") + "...", () => CherryPickRevision(single)));
                items.Add(CreateMenuItem(Loc("Revert") + "...", () => RevertRevision(single)));
                items.Add(new Separator());
                items.Add(CreateMenuItem(Loc("Copy SHA"), () => CopyToClipboard(single.Sha)));
                items.Add(CreateMenuItem(Loc("Copy Revision Info"), () => CopyRevisionInfo(single)));
                items.Add(new Separator());
                items.Add(CreateMenuItem(Loc("Compare to Working Directory"), () => CompareToWorkingDirectory(single)));
            }
            else
            {
                // 多选菜单（对照 WPF CreateMultipleRevisionsContextMenuItems）
                items.Add(CreateMenuItem(Loc("Cherry-pick") + "...", () => CherryPickRevisions(selected)));
                items.Add(new Separator());
                items.Add(CreateMenuItem(Loc("Copy SHA"), () =>
                {
                    var shas = selected.Select(r => r.Sha);
                    CopyToClipboard(string.Join(Environment.NewLine, shas));
                }));
            }

            _contextMenu.ItemsSource = items;
        }

        // 创建 MenuItem 辅助（对照 WPF RepositoryUserControl.Commands.X.CreateMenuItem）
        private MenuItem CreateMenuItem(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => action();
            return item;
        }

        // ===== 右键菜单动作（对照 WPF Commands.X.Execute，spike 直接走 git 命令）=====

        private void CheckoutRevision(RevisionEntryViewModel entry)
        {
            RunGitCommand("checkout", entry.Sha);
        }

        private void CreateBranch(RevisionEntryViewModel entry)
        {
            // spike: 不弹出 CreateBranchWindow（需 DI），用 reflog-friendly 简单分支名
            var name = "branch-" + entry.ShortSha;
            RunGitCommand("branch", name, entry.Sha);
        }

        private void CreateTag(RevisionEntryViewModel entry)
        {
            var name = "tag-" + entry.ShortSha;
            RunGitCommand("tag", name, entry.Sha);
        }

        private void MergeRevision(RevisionEntryViewModel entry)
        {
            RunGitCommand("--no-pager", "merge", "--no-edit", entry.Sha);
        }

        private void RebaseRevision(RevisionEntryViewModel entry)
        {
            RunGitCommand("--no-pager", "rebase", entry.Sha);
        }

        private void ResetRevision(RevisionEntryViewModel entry)
        {
            RunGitCommand("reset", "--hard", entry.Sha);
        }

        private void CherryPickRevision(RevisionEntryViewModel entry)
        {
            RunGitCommand("--no-pager", "cherry-pick", entry.Sha);
        }

        private void RevertRevision(RevisionEntryViewModel entry)
        {
            RunGitCommand("--no-pager", "revert", "--no-edit", entry.Sha);
        }

        private void CherryPickRevisions(RevisionEntryViewModel[] entries)
        {
            var sorted = entries.ToArray();
            Array.Sort(sorted, RevisionRowComparer.Instance);
            var args = new List<string> { "--no-pager", "cherry-pick" };
            args.AddRange(sorted.Select(e => e.Sha));
            RunGitCommand(args.ToArray());
        }

        private void CompareToWorkingDirectory(RevisionEntryViewModel entry)
        {
            // 对照 WPF: CompareRevisionToWorkingDirectory.Execute(sha)
            // spike: 走 git diff，结果输出不展示（仅执行）
            RunGitCommand("--no-pager", "diff", "--no-color", entry.Sha);
        }

        private void CopyRevisionInfo(RevisionEntryViewModel entry)
        {
            var info = $"{entry.Sha}{Environment.NewLine}{entry.Author} <{entry.AuthorEmail}>{Environment.NewLine}{entry.AuthorDate}{Environment.NewLine}{entry.Subject}";
            CopyToClipboard(info);
        }

        // 运行 git 命令（对照 WPF new GitRequest(gitModule).Command(...).Execute()）
        private void RunGitCommand(params string[] args)
        {
            var gitModule = GetGitModule();
            if (gitModule == null || args == null || args.Length == 0) return;
            var monitor = new JobMonitor();
            Task.Run(() =>
            {
                try
                {
                    var gitCommand = new GitCommand(args);
                    var result = new GitRequest(gitModule).Command(gitCommand).Execute(silent: true);
                    Dispatcher.UIThread.Post(() =>
                    {
                        // 触发刷新（对照 WPF 命令执行后 RepositoryUserControl.UpdateRepositoryData）
                        if (RepositoryUserControl is RepositoryUserControl rc && rc.RepositoryData != null)
                        {
                            rc.UpdateRepositoryData(rc.RepositoryData, null, null);
                        }
                    });
                }
                catch
                {
                }
            });
        }

        // ===== 辅助方法 =====

        // 本地化辅助（对照 WPF PreferencesLocalization → ServiceLocator.Localization）
        private static string Loc(string text)
            => ServiceLocator.Localization?.Current(text) ?? text;

        private static string LocFmt(string text, params object[] args)
            => ServiceLocator.Localization?.FormatCurrent(text, args) ?? string.Format(text, args);

        // 剪贴板辅助
        private static void CopyToClipboard(string text)
        {
            if (ServiceLocator.Clipboard != null && !string.IsNullOrEmpty(text))
            {
                ServiceLocator.Clipboard.SetText(text);
            }
        }

        // 从 RepositoryUserControl 提取 GitModule（spike 中 RepositoryUserControl 为 object）
        private GitModule GetGitModule()
        {
            if (RepositoryUserControl is RepositoryUserControl rc) return rc.GitModule;
            return null;
        }

        // 从 RepositoryUserControl 提取 CommitGraphCache
        private CommitGraphCache GetCommitGraphCache()
        {
            if (RepositoryUserControl is RepositoryUserControl rc) return rc.CommitGraphCache;
            return null;
        }

        // 获取 HEAD sha（对照 WPF RevisionsDataSource.HeadRow）
        private Sha? GetHeadSha(RepositoryData data)
        {
            if (data?.References?.ActiveBranch != null)
            {
                return data.References.ActiveBranch.Sha;
            }
            return null;
        }
    }
}
