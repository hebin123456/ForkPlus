using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 5.2 完整迁移版：Avalonia 版 SidebarUserControl。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/SidebarUserControl.xaml.cs（2714 行）：
    //   - WPF 构造函数创建 7 个 SidebarGroupItem（Pinned/Branches/Remotes/Tags/Stashes/Submodules/Worktrees）
    //     挂到 _root（FolderSidebarItem）
    //   - WPF Initialize(RepositoryUserControl) 订阅事件 + 注册 key bindings
    //   - WPF UpdateRepositoryData(RepositoryData) 增量 diff 更新（Diff<T>）
    //   - WPF 10+ 个右键菜单生成方法（CreateLocalBranchContextMenuItems 等）
    //   - WPF Reload 增量 diff 更新侧栏树 + 过滤 + 展开状态恢复
    //   - WPF SelectReference / RevealActiveBranch / Filter 等公共 API
    //
    // Avalonia 11 适配：
    //   - UserControl 基类不变
    //   - Visibility.Collapsed/Visible → IsVisible = false/true
    //   - TreeView 用 TreeDataTemplate + ItemsSource 绑定（替代 WPF HierarchicalDataTemplate）
    //   - Dispatcher.Invoke → Dispatcher.UIThread.Post
    //   - OnLoaded override → Loaded += 事件
    //   - MouseDoubleClick → DoubleTapped
    //   - PreferencesLocalization → ServiceLocator.Localization
    //   - MainWindow.Instance → 注入回调 Action<string>
    //   - JobQueue → Task.Run + Dispatcher.UIThread.Post
    //   - Image.Show()/Hide()/Collapse() → IsVisible = true/false/false
    //
    // spike 简化策略：
    //   1. ViewModel 层：内联简化 POCO（SidebarNode 及子类），不迁移 WPF 的 INotifyPropertyChanged 复杂逻辑
    //   2. 图标：用 emoji TextBlock（Branch=🌿 / Tag=🏷 / Remote=☁ / Submodule=📦 / Stash=📥 / Worktree=🗂 / Folder=📁）
    //   3. 搜索框：普通 TextBox + TextChanged 事件 + Filter(searchText) 方法（替代 FilterTextBox 自定义控件）
    //   4. RepositoryUserControl 依赖：通过 Initialize(RepositoryUserControl) 注入
    //   5. MainWindow.Instance 依赖：通过注入回调 Action<string> 替代
    //   6. 拖拽：省略拖拽重排（TreeView 原生支持拖拽选区但不再排序）
    //   7. Reload：简化为全量重建树（不做增量 diff，视觉结果一致）
    //   8. Adorner 拖拽视觉反馈：省略
    //
    // 迁移的公共 API（对照 WPF）：
    //   - Initialize(RepositoryUserControl)            ← WPF Initialize
    //   - SetRepositoryViewMode(string)                ← WPF SetRepositoryViewMode
    //   - Refresh(RepositoryData)                      ← WPF UpdateRepositoryData
    //   - SetRepositoryReferences(RepositoryReferences)← WPF 内部 UpdateSidebarItems 引用部分
    //   - SetRepositoryRemotes(RepositoryRemotes)      ← WPF 内部 UpdateSidebarItems 远端部分
    //   - SetSubmodules(Submodule[])                   ← WPF 内部 UpdateSidebarItems 子模块部分
    //   - SetWorktrees(Worktree[])                     ← WPF 内部 UpdateSidebarItems worktree 部分
    //   - SetStashes(StashRevision[])                  ← WPF 内部 UpdateSidebarItems stash 部分
    //   - Clear()                                      ← WPF 清空逻辑
    //   - Filter(string)                               ← WPF UpdateFilter
    //   - SelectReference(string)                      ← WPF 内部选择逻辑
    //   - RefreshTitle()                               ← WPF RefreshTitle
    //   - ApplyLocalization()                          ← WPF ApplyLocalization
    //   - ActivateRepositoryTab()                      ← WPF ActivateRepositoryTab
    //   - ActivateSearchTab()                          ← WPF ActivateSearchTab
    //   - RevealActiveBranch()                         ← WPF RevealActiveBranch
    //   - UpdateRepositoryStatus(object)               ← WPF UpdateRepositoryStatus
    //   公共属性：SelectedItem / SelectedReference / IsFiltering
    public partial class SidebarUserControl : UserControl
    {
        // ===== 字段（对照 WPF 私有字段）=====

        private readonly IServiceProvider _serviceProvider;
        private RepositoryUserControl _repositoryUserControl;

        // 对照 WPF 7 个 SidebarGroupItem + _root（FolderSidebarItem）
        private readonly ObservableCollection<SidebarNode> _rootNodes;
        private readonly GroupNode _pinned;
        private readonly GroupNode _branches;
        private readonly GroupNode _remotes;
        private readonly GroupNode _tags;
        private readonly GroupNode _stashes;
        private readonly GroupNode _submodules;
        private readonly GroupNode _worktrees;

        // 对照 WPF _repositoryData = RepositoryData.Empty
        private RepositoryData _repositoryData = RepositoryData.Empty;

        // 对照 WPF 分片数据（Set* 方法更新后用于重建树）
        private RepositoryReferences _references;
        private RepositoryRemotes _remotesData;
        private Submodule[] _submodulesData = new Submodule[0];
        private Worktree[] _worktreesData = new Worktree[0];
        private StashRevision[] _stashesData = new StashRevision[0];

        // 对照 WPF _refreshFilterAction / _filterText
        private string _filterText = string.Empty;

        // 对照 WPF _initialized / _updateRepositoryDataInProgress / _selectionInProgress
        private bool _initialized;
        private bool _updateRepositoryDataInProgress;
        private bool _selectionInProgress;

        // ===== 注入回调（替代 MainWindow.Instance 依赖）=====

        // 仓库操作回调
        public Action<string> OpenRepositoryCallback { get; set; }
        public Action<string> CloseTabCallback { get; set; }

        // 分支操作回调（对照 WPF RepositoryUserControl.Commands.*）
        public Action<LocalBranch> CheckoutBranchCallback { get; set; }
        public Action<LocalBranch> RenameBranchCallback { get; set; }
        public Action<LocalBranch[]> DeleteBranchesCallback { get; set; }
        public Action<LocalBranch, Remote> PushBranchCallback { get; set; }
        public Action<LocalBranch> PullBranchCallback { get; set; }
        public Action<Branch, LocalBranch> MergeBranchCallback { get; set; }
        public Action<LocalBranch, Branch> RebaseBranchCallback { get; set; }
        public Action<RemoteBranch> CheckoutRemoteBranchCallback { get; set; }
        public Action<RemoteBranch[]> DeleteRemoteBranchesCallback { get; set; }

        // Tag 操作回调
        public Action<Tag> DeleteTagCallback { get; set; }
        public Action<Tag, Remote> PushTagCallback { get; set; }
        public Action<Tag> CheckoutTagCallback { get; set; }

        // Submodule 操作回调
        public Action<Submodule> OpenSubmoduleCallback { get; set; }
        public Action<Submodule> UpdateSubmoduleCallback { get; set; }
        public Action<Submodule> DeleteSubmoduleCallback { get; set; }

        // Stash 操作回调
        public Action<StashRevision> ApplyStashCallback { get; set; }
        public Action<StashRevision> PopStashCallback { get; set; }
        public Action<StashRevision> DeleteStashCallback { get; set; }

        // Worktree 操作回调
        public Action<Worktree> OpenWorktreeCallback { get; set; }
        public Action<Worktree> DeleteWorktreeCallback { get; set; }

        // ===== 公共属性（对照 WPF SidebarTreeView.SelectedItem 等）=====

        // 对照 WPF SidebarTreeView.SelectedItem
        public SidebarNode SelectedItem { get; private set; }

        // 对照 WPF 选中引用（ReferenceSidebarItem.Reference）
        public Reference SelectedReference { get; private set; }

        // 对照 WPF SidebarTreeView.FilterString 非空判断
        public bool IsFiltering => !string.IsNullOrEmpty(_filterText);

        // ===== 构造函数 =====

        // 对照 WPF SidebarUserControl()：创建 7 个 SidebarGroupItem 挂到 _root
        // spike 版构造函数注入 IServiceProvider（DI 容器）
        public SidebarUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();

            // 对照 WPF: _root = new FolderSidebarItem("", null, this)
            _rootNodes = new ObservableCollection<SidebarNode>();

            // 对照 WPF: CreateSidebarGroupItem(SidebarGroupItem.Group.*)
            _pinned = new GroupNode(Localize("Pinned"), "📌");
            _branches = new GroupNode(Localize("Branches"), "🌿");
            _remotes = new GroupNode(Localize("Remotes"), "☁");
            _tags = new GroupNode(Localize("Tags"), "🏷");
            _stashes = new GroupNode(Localize("Stashes"), "📥");
            _submodules = new GroupNode(Localize("Submodules"), "📦");
            _worktrees = new GroupNode(Localize("Worktrees"), "🗂");

            // 对照 WPF: _root.Children.Add(_branches) 等
            _rootNodes.Add(_branches);
            _rootNodes.Add(_remotes);
            _rootNodes.Add(_tags);
            _rootNodes.Add(_stashes);
            _rootNodes.Add(_submodules);

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            // 对照 WPF: InitializeComponent 后的事件订阅在 Initialize 中完成
            // spike 版 Loaded 时把 _rootNodes 绑定到 TreeView
            if (SidebarTreeView != null && SidebarTreeView.ItemsSource == null)
            {
                SidebarTreeView.ItemsSource = _rootNodes;
            }
        }

        // ===== 核心公共 API =====

        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl)
        //   订阅事件 + 注册 key bindings + 设置 RootItem
        public void Initialize(RepositoryUserControl repositoryUserControl)
        {
            _repositoryUserControl = repositoryUserControl;

            // 对照 WPF: SidebarTreeView.RootItem = _root
            if (SidebarTreeView != null)
            {
                SidebarTreeView.ItemsSource = _rootNodes;
                SidebarTreeView.SelectionChanged += SidebarTreeView_SelectionChanged;
                SidebarTreeView.DoubleTapped += SidebarTreeView_DoubleTapped;
            }

            // 对照 WPF: FilterTextBox.FilterRequestChanged += ...
            // spike 版用 TextChanged 事件（在 axaml 中已绑定）

            _initialized = true;
        }

        // 对照 WPF: public void SetRepositoryViewMode(RepositoryViewMode viewMode)
        //   RevisionViewMode → AllCommitsRadioButton.IsChecked = true
        //   CommitViewMode → ChangesRadioButton.IsChecked = true
        public void SetRepositoryViewMode(string viewMode)
        {
            if (viewMode == "RevisionViewMode" || viewMode == "AllCommits")
            {
                if (AllCommitsRadioButton != null) AllCommitsRadioButton.IsChecked = true;
            }
            else if (viewMode == "CommitViewMode" || viewMode == "Changes")
            {
                if (ChangesRadioButton != null) ChangesRadioButton.IsChecked = true;
            }
        }

        // 对照 WPF: public void UpdateRepositoryData(RepositoryData repositoryData)
        //   → Reload(repositoryData, forceRefresh: false, SidebarTreeView.FilterString)
        public void Refresh(RepositoryData data)
        {
            Reload(data, forceRefresh: false, _filterText);
        }

        // 对照 WPF UpdateRepositoryData 别名
        public void UpdateRepositoryData(RepositoryData data)
        {
            Refresh(data);
        }

        // 对照 WPF: public void UpdateRepositoryStatus(RepositoryStatus repositoryStatus)
        //   更新 Changes 文本 + 子模块 dirty 状态
        public void UpdateRepositoryStatus(object repositoryStatus)
        {
            // spike 版：更新 Changes 文本（对照 WPF ChangesTextBlock.Text）
            // 完整的 RepositoryStatus 类型迁移留待 Phase 3.x 后续
            if (ChangesTextBlock != null)
            {
                ChangesTextBlock.Text = Localize("Changes");
            }
        }

        // 设置引用数据（对照 WPF UpdateSidebarItems 引用部分）
        public void SetRepositoryReferences(RepositoryReferences refs)
        {
            _references = refs;
            RebuildTree();
        }

        // 设置远程数据（对照 WPF UpdateSidebarItems 远端部分）
        public void SetRepositoryRemotes(RepositoryRemotes remotes)
        {
            _remotesData = remotes;
            RebuildTree();
        }

        // 设置子模块（对照 WPF UpdateSidebarItems 子模块部分）
        public void SetSubmodules(Submodule[] submodules)
        {
            _submodulesData = submodules ?? new Submodule[0];
            RebuildTree();
        }

        // 设置 worktree（对照 WPF UpdateSidebarItems worktree 部分）
        public void SetWorktrees(Worktree[] worktrees)
        {
            _worktreesData = worktrees ?? new Worktree[0];
            RebuildTree();
        }

        // 设置 stash（对照 WPF UpdateSidebarItems stash 部分）
        // 注：WPF 用 StashRevision（RepositoryStashes.Items 类型），任务描述中 Stash 即 StashRevision
        public void SetStashes(StashRevision[] stashes)
        {
            _stashesData = stashes ?? new StashRevision[0];
            RebuildTree();
        }

        // 清空侧栏（对照 WPF 清空逻辑）
        public void Clear()
        {
            _repositoryData = RepositoryData.Empty;
            _references = null;
            _remotesData = null;
            _submodulesData = new Submodule[0];
            _worktreesData = new Worktree[0];
            _stashesData = new StashRevision[0];
            _filterText = string.Empty;

            _pinned.Children.Clear();
            _branches.Children.Clear();
            _remotes.Children.Clear();
            _tags.Children.Clear();
            _stashes.Children.Clear();
            _submodules.Children.Clear();
            _worktrees.Children.Clear();

            SelectedItem = null;
            SelectedReference = null;

            if (FilterTextBox != null) FilterTextBox.Text = string.Empty;
            if (RepositoryNameText != null) RepositoryNameText.Text = Localize("(no repository)");
        }

        // 对照 WPF: private void UpdateFilter(string filterString)
        //   → Reload(_repositoryData, forceRefresh: false, filterString)
        public void Filter(string searchText)
        {
            _filterText = searchText ?? string.Empty;
            Reload(_repositoryData, forceRefresh: false, _filterText);
        }

        // 选中引用（对照 WPF SidebarTreeView_SelectionChanged 中选择逻辑）
        //   按 fullReference 查找节点并选中
        public void SelectReference(string fullReference)
        {
            if (string.IsNullOrEmpty(fullReference))
            {
                return;
            }

            SidebarNode node = FindNodeByReference(_rootNodes, fullReference);
            if (node != null)
            {
                _selectionInProgress = true;
                SelectedItem = node;
                if (node is BranchNode branchNode)
                {
                    SelectedReference = branchNode.Branch;
                }
                else if (node is RemoteBranchNode remoteBranchNode)
                {
                    SelectedReference = remoteBranchNode.Branch;
                }
                else if (node is TagNode tagNode)
                {
                    SelectedReference = tagNode.Tag;
                }

                // 展开所有祖先节点
                ExpandAncestors(node);

                // 选中 TreeViewItem
                if (SidebarTreeView != null)
                {
                    SidebarTreeView.SelectedItem = node;
                }
                _selectionInProgress = false;
            }
        }

        // 对照 WPF: public void RefreshTitle()
        //   读取 RepositoryUserControl.RepositoryName / ParentRepositoryName，写入顶部仓库名栏
        public void RefreshTitle()
        {
            if (_repositoryUserControl == null)
            {
                return;
            }
            if (RepositoryParentNameText != null)
            {
                RepositoryParentNameText.Text = _repositoryUserControl.ParentRepositoryName ?? "";
            }
            if (RepositoryNameText != null)
            {
                RepositoryNameText.Text = _repositoryUserControl.RepositoryName
                    ?? Localize("(no repository)");
            }
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            if (AllCommitsRadioButton != null) AllCommitsRadioButton.Content = Localize("All Commits");
            if (ChangesRadioButton != null) ChangesTextBlock.Text = Localize("Changes");
            // Avalonia 用 ToolTip.SetTip 静态方法设置工具提示（对照 WPF ToolTip property）
            if (BranchesRadioButton != null) ToolTip.SetTip(BranchesRadioButton, Localize("Branches"));
            if (SearchRadioButton != null) ToolTip.SetTip(SearchRadioButton, Localize("Search Commits"));
            if (ServiceRadioButton != null) ToolTip.SetTip(ServiceRadioButton, Localize("Pull Requests"));

            // 刷新分组标题
            _pinned.Name = Localize("Pinned");
            _branches.Name = Localize("Branches");
            _remotes.Name = Localize("Remotes");
            _tags.Name = Localize("Tags");
            _stashes.Name = Localize("Stashes");
            _submodules.Name = Localize("Submodules");
            _worktrees.Name = Localize("Worktrees");
        }

        // 对照 WPF: public void ActivateRepositoryTab()
        //   BranchesTabItem.IsSelected = true
        public void ActivateRepositoryTab()
        {
            if (SidebarTabControl != null && BranchesTabItem != null)
            {
                SidebarTabControl.SelectedItem = BranchesTabItem;
            }
        }

        // 对照 WPF: public void ActivateSearchTab()
        //   SearchTabItem.IsSelected = true; SearchTabItem.OnActivated()
        public void ActivateSearchTab()
        {
            if (SidebarTabControl != null && SearchTabItem != null)
            {
                SidebarTabControl.SelectedItem = SearchTabItem;
            }
        }

        // 对照 WPF: public void RevealActiveBranch()
        //   查找活动分支项并滚动到视图
        public void RevealActiveBranch()
        {
            if (BranchesTabItem?.IsSelected != true)
            {
                ActivateRepositoryTab();
            }

            BranchNode activeBranch = FindActiveBranchItem(_pinned);
            if (activeBranch == null)
            {
                activeBranch = FindActiveBranchItem(_branches);
            }

            if (activeBranch != null && SidebarTreeView != null)
            {
                // 展开所有祖先节点
                ExpandAncestors(activeBranch);
                SidebarTreeView.SelectedItem = activeBranch;
            }
        }

        // ===== 内部重建逻辑（对照 WPF Reload + UpdateSidebarItems）=====

        // 对照 WPF: private void Reload(RepositoryData newRepositoryData, bool forceRefresh, string sidebarFilterString)
        //   spike 版简化为全量重建（不做增量 diff，视觉结果一致）
        private void Reload(RepositoryData newRepositoryData, bool forceRefresh, string sidebarFilterString)
        {
            RepositoryData oldRepositoryData = _repositoryData;

            // 对照 WPF: 数据未变化则跳过
            if (!forceRefresh && sidebarFilterString == _filterText &&
                oldRepositoryData == newRepositoryData)
            {
                return;
            }

            _updateRepositoryDataInProgress = true;

            if (newRepositoryData != null)
            {
                _repositoryData = newRepositoryData;
                // 从 RepositoryData 同步分片数据
                _references = newRepositoryData.References;
                _remotesData = newRepositoryData.Remotes;
                _submodulesData = newRepositoryData.Submodules?.Items ?? new Submodule[0];
                _worktreesData = newRepositoryData.Worktrees?.Items ?? new Worktree[0];
                _stashesData = newRepositoryData.Stashes?.Items ?? new StashRevision[0];
            }

            UpdateVisibleTabs();
            RebuildTree();

            // 对照 WPF: 活动分支变化时 RevealActiveBranch
            if (oldRepositoryData?.References?.ActiveBranch != _repositoryData?.References?.ActiveBranch)
            {
                RevealActiveBranch();
            }

            _updateRepositoryDataInProgress = false;
        }

        // 对照 WPF: private void UpdateVisibleTabs(RepositoryData repositoryData)
        //   有 Account remote 时显示 Service 按钮，否则隐藏
        private void UpdateVisibleTabs()
        {
            Remote[] accountRemotes = _remotesData?.Items?
                .Where(r => r.Account != null).ToArray() ?? new Remote[0];

            if (accountRemotes.Length > 0)
            {
                if (ServiceRadioButton != null) ServiceRadioButton.IsVisible = true;
            }
            else
            {
                if (ServiceTabItem?.IsSelected ?? false)
                {
                    ActivateRepositoryTab();
                }
                if (ServiceRadioButton != null) ServiceRadioButton.IsVisible = false;
            }
        }

        // 对照 WPF: private void UpdateSidebarItems(...)
        //   spike 版全量重建树（清空 + 重新填充各分组）
        private void RebuildTree()
        {
            if (!_initialized)
            {
                return;
            }

            bool filterEnabled = !string.IsNullOrEmpty(_filterText);

            // 清空所有分组
            _pinned.Children.Clear();
            _branches.Children.Clear();
            _remotes.Children.Clear();
            _tags.Children.Clear();
            _stashes.Children.Clear();
            _submodules.Children.Clear();
            _worktrees.Children.Clear();

            // 1. Pinned（对照 WPF _pinned 填充）
            if (_references?.Pinned != null)
            {
                foreach (Reference reference in _references.Pinned)
                {
                    AddPinnedNode(reference, filterEnabled);
                }
            }

            // 2. Branches（对照 WPF _branches 填充，含 "/" 分层文件夹）
            if (_references?.LocalBranches != null)
            {
                foreach (LocalBranch branch in _references.LocalBranches)
                {
                    AddBranchNode(_branches, branch, filterEnabled);
                }
            }

            // 3. Remotes + Remote Branches（对照 WPF _remotes 填充）
            if (_references?.RemoteBranches != null)
            {
                foreach (RemoteBranch remoteBranch in _references.RemoteBranches)
                {
                    AddRemoteBranchNode(remoteBranch, filterEnabled);
                }
            }

            // 4. Tags（对照 WPF _tags 填充）
            if (_references?.Tags != null)
            {
                foreach (Tag tag in _references.Tags)
                {
                    AddTagNode(tag, filterEnabled);
                }
            }

            // 5. Stashes（对照 WPF _stashes 填充）
            foreach (StashRevision stash in _stashesData)
            {
                AddStashNode(stash, filterEnabled);
            }

            // 6. Submodules（对照 WPF _submodules 填充）
            foreach (Submodule submodule in _submodulesData)
            {
                AddSubmoduleNode(submodule, filterEnabled);
            }

            // 7. Worktrees（对照 WPF _worktrees 填充）
            RebuildWorktrees();

            // 更新 _rootNodes 中分组的可见性（有子节点则显示）
            UpdateGroupVisibility();

            // 设置默认展开状态（对照 WPF _branches.IsExpanded = true 等）
            _branches.IsExpanded = true;
            _remotes.IsExpanded = true;
            _stashes.IsExpanded = true;
        }

        // 添加 Pinned 节点（对照 WPF AddItem to _pinned）
        private void AddPinnedNode(Reference reference, bool filterEnabled)
        {
            if (filterEnabled && !MatchesFilter(reference.Name))
            {
                return;
            }

            if (reference is LocalBranch localBranch)
            {
                _pinned.Children.Add(new BranchNode(localBranch));
            }
            else if (reference is RemoteBranch remoteBranch)
            {
                _pinned.Children.Add(new RemoteBranchNode(remoteBranch));
            }
            else if (reference is Tag tag)
            {
                _pinned.Children.Add(new TagNode(tag));
            }
        }

        // 添加本地分支节点（对照 WPF AddItem to _branches，含 "/" 分层）
        private void AddBranchNode(GroupNode parent, LocalBranch branch, bool filterEnabled)
        {
            if (filterEnabled && !MatchesFilter(branch.Name))
            {
                return;
            }

            // 对照 WPF: name.Split('/') 分层文件夹
            string[] parts = branch.Name.Split('/');
            if (parts.Length == 1)
            {
                parent.Children.Add(new BranchNode(branch));
            }
            else
            {
                // 创建文件夹层级
                GroupNode current = parent;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    GroupNode folder = current.Children.OfType<GroupNode>()
                        .FirstOrDefault(c => c.Name == parts[i] && c.IsFolder);
                    if (folder == null)
                    {
                        folder = new GroupNode(parts[i], "📁") { IsFolder = true };
                        current.Children.Add(folder);
                    }
                    current = folder;
                }
                // 叶子节点用短名
                var leafBranch = new BranchNode(branch) { Name = parts[parts.Length - 1] };
                current.Children.Add(leafBranch);
            }
        }

        // 添加远程分支节点（对照 WPF AddRemoteBranchItem）
        private void AddRemoteBranchNode(RemoteBranch remoteBranch, bool filterEnabled)
        {
            if (filterEnabled && !MatchesFilter(remoteBranch.Name))
            {
                return;
            }

            // 按远程名分组（对照 WPF: folderItem.Title == remoteBranch.Remote）
            GroupNode remoteFolder = _remotes.Children.OfType<GroupNode>()
                .FirstOrDefault(c => c.Name == remoteBranch.Remote);
            if (remoteFolder == null)
            {
                remoteFolder = new GroupNode(remoteBranch.Remote, "☁");
                _remotes.Children.Add(remoteFolder);
            }
            remoteFolder.Children.Add(new RemoteBranchNode(remoteBranch));
        }

        // 添加 Tag 节点（对照 WPF AddItem to _tags）
        private void AddTagNode(Tag tag, bool filterEnabled)
        {
            if (filterEnabled && !MatchesFilter(tag.Name))
            {
                return;
            }
            _tags.Children.Add(new TagNode(tag));
        }

        // 添加 Stash 节点（对照 WPF new StashSidebarItem）
        private void AddStashNode(StashRevision stash, bool filterEnabled)
        {
            if (filterEnabled && !MatchesFilter(stash.Message))
            {
                return;
            }
            _stashes.Children.Add(new StashNode(stash));
        }

        // 添加 Submodule 节点（对照 WPF new SubmoduleSidebarItem）
        private void AddSubmoduleNode(Submodule submodule, bool filterEnabled)
        {
            if (filterEnabled && !MatchesFilter(submodule.Path))
            {
                return;
            }
            _submodules.Children.Add(new SubmoduleNode(submodule));
        }

        // 重建 Worktrees 分组（对照 WPF UpdateSidebarItems worktree 部分）
        private void RebuildWorktrees()
        {
            // 对照 WPF: worktrees.MainWorktree.HasValue || items.Length > 0
            bool hasWorktrees = (_worktreesData != null && _worktreesData.Length > 0);

            if (hasWorktrees)
            {
                // 对照 WPF: _worktrees.Children.Clear() + 添加各 worktree
                foreach (Worktree worktree in _worktreesData)
                {
                    _worktrees.Children.Add(new WorktreeNode(worktree));
                }

                // 对照 WPF: _root.Children.Insert(0, _worktrees)
                if (!_rootNodes.Contains(_worktrees))
                {
                    _rootNodes.Insert(0, _worktrees);
                }
            }
            else
            {
                // 对照 WPF: _root.Children.Remove(_worktrees)
                _rootNodes.Remove(_worktrees);
            }

            // 对照 WPF: _pinned 显示/隐藏
            if (_pinned.Children.Count > 0)
            {
                if (!_rootNodes.Contains(_pinned))
                {
                    int index = _rootNodes.Contains(_worktrees) ? 1 : 0;
                    _rootNodes.Insert(index, _pinned);
                }
            }
            else
            {
                _rootNodes.Remove(_pinned);
            }
        }

        // 更新分组可见性（对照 WPF 分组有子节点才显示）
        private void UpdateGroupVisibility()
        {
            _branches.IsVisible = _branches.Children.Count > 0;
            _remotes.IsVisible = _remotes.Children.Count > 0;
            _tags.IsVisible = _tags.Children.Count > 0;
            _stashes.IsVisible = _stashes.Children.Count > 0;
            _submodules.IsVisible = _submodules.Children.Count > 0;
            _worktrees.IsVisible = _worktrees.Children.Count > 0;
        }

        // ===== 查找辅助方法（对照 WPF FindActiveBranchItem / FindFolder 等）=====

        // 对照 WPF: private LocalBranchSidebarItem FindActiveBranchItem(FolderSidebarItem parent)
        private BranchNode FindActiveBranchItem(GroupNode parent)
        {
            foreach (SidebarNode child in parent.Children)
            {
                if (child is BranchNode branchNode && branchNode.Branch.IsActive)
                {
                    return branchNode;
                }
                if (child is GroupNode groupNode)
                {
                    BranchNode found = FindActiveBranchItem(groupNode);
                    if (found != null) return found;
                }
            }
            return null;
        }

        // 按 fullReference 查找节点
        private SidebarNode FindNodeByReference(IEnumerable<SidebarNode> nodes, string fullReference)
        {
            foreach (SidebarNode node in nodes)
            {
                string nodeRef = GetNodeFullReference(node);
                if (nodeRef == fullReference)
                {
                    return node;
                }
                if (node.Children.Count > 0)
                {
                    SidebarNode found = FindNodeByReference(node.Children, fullReference);
                    if (found != null) return found;
                }
            }
            return null;
        }

        // 获取节点的 FullReference
        private static string GetNodeFullReference(SidebarNode node)
        {
            if (node is BranchNode branchNode) return branchNode.Branch.FullReference;
            if (node is RemoteBranchNode remoteBranchNode) return remoteBranchNode.Branch.FullReference;
            if (node is TagNode tagNode) return tagNode.Tag.FullReference;
            return null;
        }

        // 展开所有祖先节点（对照 WPF: item.Ancestors().IsExpanded = true）
        private void ExpandAncestors(SidebarNode node)
        {
            // 遍历 rootNodes，展开包含该节点的路径
            foreach (SidebarNode root in _rootNodes)
            {
                if (ExpandAncestorsRecursive(root, node))
                {
                    break;
                }
            }
        }

        private bool ExpandAncestorsRecursive(SidebarNode current, SidebarNode target)
        {
            if (current == target) return true;
            foreach (SidebarNode child in current.Children)
            {
                if (ExpandAncestorsRecursive(child, target))
                {
                    if (current is GroupNode groupNode) groupNode.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }

        // ===== 过滤辅助（对照 WPF SidebarTreeView.FilterString 匹配）=====

        private bool MatchesFilter(string name)
        {
            if (string.IsNullOrEmpty(_filterText)) return true;
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ===== 本地化辅助（对照 WPF PreferencesLocalization.Current → ServiceLocator.Localization.Current）=====

        private static string Localize(string text)
        {
            try
            {
                return ServiceLocator.Localization?.Current(text) ?? text;
            }
            catch
            {
                return text;
            }
        }

        // ===== 右键菜单生成（对照 WPF Create*ContextMenuItems 系列）=====

        // 对照 WPF: SidebarTreeView_ContextMenuOpening
        //   根据选中节点类型构建对应右键菜单
        private void BuildContextMenu(SidebarNode node)
        {
            if (node == null) return;

            var menu = new ContextMenu();
            menu.Items.Clear();

            switch (node)
            {
                case BranchNode branchNode:
                    BuildBranchContextMenu(menu, branchNode);
                    break;
                case RemoteBranchNode remoteBranchNode:
                    BuildRemoteBranchContextMenu(menu, remoteBranchNode);
                    break;
                case TagNode tagNode:
                    BuildTagContextMenu(menu, tagNode);
                    break;
                case SubmoduleNode submoduleNode:
                    BuildSubmoduleContextMenu(menu, submoduleNode);
                    break;
                case StashNode stashNode:
                    BuildStashContextMenu(menu, stashNode);
                    break;
                case WorktreeNode worktreeNode:
                    BuildWorktreeContextMenu(menu, worktreeNode);
                    break;
                case GroupNode groupNode:
                    BuildGroupContextMenu(menu, groupNode);
                    break;
            }

            if (menu.Items.Count > 0 && SidebarTreeView != null)
            {
                SidebarTreeView.ContextMenu = menu;
            }
        }

        // 对照 WPF: CreateLocalBranchContextMenuItems
        //   Checkout / Push / Pull / Merge / Rebase / Rename / Delete
        private void BuildBranchContextMenu(ContextMenu menu, BranchNode branchNode)
        {
            LocalBranch branch = branchNode.Branch;

            // Checkout
            AddMenuItem(menu, $"Checkout '{branch.Name}'", () => CheckoutBranchCallback?.Invoke(branch));

            menu.Items.Add(new Separator());

            // Push
            AddMenuItem(menu, $"Push '{branch.Name}'...", () =>
            {
                Remote remote = _remotesData?.Items?.FirstOrDefault();
                PushBranchCallback?.Invoke(branch, remote);
            });

            // Pull
            if (!string.IsNullOrEmpty(branch.UpstreamFullReference))
            {
                AddMenuItem(menu, $"Pull '{branch.Name}'...", () => PullBranchCallback?.Invoke(branch));
            }

            menu.Items.Add(new Separator());

            // Merge（对照 WPF: ShowMergeBranchWindow）
            LocalBranch activeBranch = _references?.ActiveBranch;
            if (activeBranch != null && activeBranch != branch)
            {
                AddMenuItem(menu, $"Merge into '{activeBranch.Name}'...",
                    () => MergeBranchCallback?.Invoke(branch, activeBranch));
                AddMenuItem(menu, $"Rebase '{activeBranch.Name}' on '{branch.Name}'...",
                    () => RebaseBranchCallback?.Invoke(activeBranch, branch));
                menu.Items.Add(new Separator());
            }

            // Rename
            AddMenuItem(menu, $"Rename '{branch.Name}'...", () => RenameBranchCallback?.Invoke(branch));

            // Delete
            AddMenuItem(menu, $"Delete '{branch.Name}'...",
                () => DeleteBranchesCallback?.Invoke(new[] { branch }),
                isEnabled: activeBranch != branch);

            menu.Items.Add(new Separator());

            // Copy Branch Name
            AddMenuItem(menu, "Copy Branch Name", () =>
                Console.WriteLine($"[Sidebar] Copy branch name: {branch.Name}"));
        }

        // 对照 WPF: CreateRemoteBranchContextMenuItems
        //   Checkout / Pull / Merge / Rebase / Delete
        private void BuildRemoteBranchContextMenu(ContextMenu menu, RemoteBranchNode remoteBranchNode)
        {
            RemoteBranch branch = remoteBranchNode.Branch;

            AddMenuItem(menu, $"Checkout '{branch.Name}'",
                () => CheckoutRemoteBranchCallback?.Invoke(branch));

            menu.Items.Add(new Separator());

            LocalBranch activeBranch = _references?.ActiveBranch;
            if (activeBranch != null)
            {
                AddMenuItem(menu, $"Pull '{branch.Name}' into '{activeBranch.Name}'...",
                    () => PullBranchCallback?.Invoke(activeBranch));
                AddMenuItem(menu, $"Merge into '{activeBranch.Name}'...",
                    () => MergeBranchCallback?.Invoke(branch, activeBranch));
                AddMenuItem(menu, $"Rebase '{activeBranch.Name}' on '{branch.Name}'...",
                    () => RebaseBranchCallback?.Invoke(activeBranch, branch));
                menu.Items.Add(new Separator());
            }

            AddMenuItem(menu, $"Delete '{branch.Name}'...",
                () => DeleteRemoteBranchesCallback?.Invoke(new[] { branch }));
        }

        // 对照 WPF: CreateTagContextMenuItems
        //   Checkout / Push / Delete
        private void BuildTagContextMenu(ContextMenu menu, TagNode tagNode)
        {
            Tag tag = tagNode.Tag;

            AddMenuItem(menu, $"Checkout '{tag.Name}'", () => CheckoutTagCallback?.Invoke(tag));

            menu.Items.Add(new Separator());

            // Push
            AddMenuItem(menu, $"Push '{tag.Name}'...", () =>
            {
                Remote remote = _remotesData?.Items?.FirstOrDefault();
                PushTagCallback?.Invoke(tag, remote);
            });

            // Delete
            AddMenuItem(menu, $"Delete '{tag.Name}'...", () => DeleteTagCallback?.Invoke(tag));

            menu.Items.Add(new Separator());

            AddMenuItem(menu, "Copy Tag Name", () =>
                Console.WriteLine($"[Sidebar] Copy tag name: {tag.Name}"));
        }

        // 对照 WPF: CreateSubmoduleSidebarItemMenuItems
        //   Open / Update / Delete
        private void BuildSubmoduleContextMenu(ContextMenu menu, SubmoduleNode submoduleNode)
        {
            Submodule submodule = submoduleNode.Submodule;

            AddMenuItem(menu, $"Open '{submodule.Path}'...",
                () => OpenSubmoduleCallback?.Invoke(submodule));

            menu.Items.Add(new Separator());

            AddMenuItem(menu, $"Update '{submodule.Path}'",
                () => UpdateSubmoduleCallback?.Invoke(submodule));

            AddMenuItem(menu, $"Delete '{submodule.Path}'...",
                () => DeleteSubmoduleCallback?.Invoke(submodule));
        }

        // 对照 WPF: CreateStashContextMenuItems
        //   Apply / Pop / Delete
        private void BuildStashContextMenu(ContextMenu menu, StashNode stashNode)
        {
            StashRevision stash = stashNode.Stash;

            AddMenuItem(menu, $"Apply '{stash.Message}'...",
                () => ApplyStashCallback?.Invoke(stash));

            AddMenuItem(menu, $"Pop '{stash.Message}'...",
                () => PopStashCallback?.Invoke(stash));

            menu.Items.Add(new Separator());

            AddMenuItem(menu, $"Delete '{stash.Message}'...",
                () => DeleteStashCallback?.Invoke(stash));
        }

        // 对照 WPF: CreateWorktreeSidebarItemMenuItems
        //   Open / Delete
        private void BuildWorktreeContextMenu(ContextMenu menu, WorktreeNode worktreeNode)
        {
            Worktree worktree = worktreeNode.Worktree;

            AddMenuItem(menu, $"Open '{worktree.FriendlyName}'...",
                () => OpenWorktreeCallback?.Invoke(worktree));

            menu.Items.Add(new Separator());

            AddMenuItem(menu, $"Delete '{worktree.FriendlyName}'...",
                () => DeleteWorktreeCallback?.Invoke(worktree),
                isEnabled: !worktree.IsActive);
        }

        // 对照 WPF: Create*SidebarItemGroupMenuItems（分组节点菜单）
        private void BuildGroupContextMenu(ContextMenu menu, GroupNode groupNode)
        {
            if (groupNode == _branches)
            {
                // 对照 WPF: CreateBranchesSidebarItemGroupMenuItems
                AddMenuItem(menu, "Create Branch...", () => { });
                menu.Items.Add(new Separator());
                AddMenuItem(menu, "Sort: Alphabetically", () => { });
                AddMenuItem(menu, "Sort: Recently Used", () => { });
            }
            else if (groupNode == _tags)
            {
                // 对照 WPF: CreateTagsSidebarItemGroupMenuItems
                AddMenuItem(menu, "Create Tag...", () => { });
            }
            else if (groupNode == _remotes)
            {
                // 对照 WPF: CreateRemoteSidebarItemGroupMenuItems
                AddMenuItem(menu, "Add Remote...", () => { });
            }
            else if (groupNode == _submodules)
            {
                // 对照 WPF: CreateSubmoduleSidebarItemGroupMenuItems
                AddMenuItem(menu, "Add Submodule...", () => { });
                AddMenuItem(menu, "Update Submodules", () => { });
            }
            else if (groupNode == _worktrees)
            {
                // 对照 WPF: CreateWorktreesSidebarItemGroupMenuItems
                AddMenuItem(menu, "Create Worktree...", () => { });
            }
        }

        // 添加菜单项辅助方法
        private static void AddMenuItem(ContextMenu menu, string header, Action onClick, bool isEnabled = true)
        {
            var item = new MenuItem { Header = header, IsEnabled = isEnabled };
            item.Click += (_, _) => onClick?.Invoke();
            menu.Items.Add(item);
        }

        // ===== 事件处理器 =====

        // 对照 WPF: RepoSettingsDropdownButtonContextMenu_Opened
        private void RepoSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Sidebar] Repo settings clicked");
        }

        // 对照 WPF: Changes_Selected → RepositoryUserControl.SetRepositoryViewMode(CommitViewMode)
        private void ChangesRadioButton_Click(object sender, RoutedEventArgs e)
        {
            _repositoryUserControl?.SetRepositoryViewMode(RepositoryContentUserControl.CommitViewMode);
        }

        // 对照 WPF: AllCommits_Selected → RepositoryUserControl.SetRepositoryViewMode(RevisionViewMode)
        private void AllCommitsRadioButton_Click(object sender, RoutedEventArgs e)
        {
            _repositoryUserControl?.SetRepositoryViewMode(RepositoryContentUserControl.RevisionViewMode);
        }

        // 对照 WPF: 切换到 BranchesTabItem
        private void BranchesRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (SidebarTabControl != null && BranchesTabItem != null)
            {
                SidebarTabControl.SelectedItem = BranchesTabItem;
            }
        }

        // 对照 WPF: 切换到 SearchTabItem
        private void SearchRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (SidebarTabControl != null && SearchTabItem != null)
            {
                SidebarTabControl.SelectedItem = SearchTabItem;
            }
        }

        // 对照 WPF: 切换到 ServiceTabItem
        private void ServiceRadioButton_Click(object sender, RoutedEventArgs e)
        {
            if (SidebarTabControl != null && ServiceTabItem != null)
            {
                SidebarTabControl.SelectedItem = ServiceTabItem;
            }
        }

        // 对照 WPF: TabControl_SelectionChanged
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tabControl && tabControl.SelectedItem is TabItem item)
            {
                Console.WriteLine($"[Sidebar] Tab changed: {item.Header}");
            }
        }

        // 对照 WPF: FilterTextBox.FilterRequestChanged → _refreshFilterAction.InvokeWithDelay
        //   spike 版用 TextChanged + 直接 Filter（无防抖，简化）
        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                _filterText = textBox.Text ?? string.Empty;
                Reload(_repositoryData, forceRefresh: false, _filterText);
            }
        }

        // 对照 WPF: SidebarTreeView_SelectionChanged
        //   选择变更时更新 SelectedItem / SelectedReference
        private void SidebarTreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updateRepositoryDataInProgress || _selectionInProgress)
            {
                return;
            }

            if (SidebarTreeView?.SelectedItem is SidebarNode node)
            {
                SelectedItem = node;
                SelectedReference = GetNodeReference(node);
                BuildContextMenu(node);
            }
        }

        // 对照 WPF: SidebarTreeView_MouseDoubleClick
        //   双击节点执行默认操作（checkout branch / open submodule / apply stash 等）
        private void SidebarTreeView_DoubleTapped(object sender, RoutedEventArgs e)
        {
            if (SidebarTreeView?.SelectedItem is not SidebarNode node)
            {
                return;
            }

            switch (node)
            {
                case BranchNode branchNode:
                    CheckoutBranchCallback?.Invoke(branchNode.Branch);
                    break;
                case RemoteBranchNode remoteBranchNode:
                    CheckoutRemoteBranchCallback?.Invoke(remoteBranchNode.Branch);
                    break;
                case TagNode tagNode:
                    CheckoutTagCallback?.Invoke(tagNode.Tag);
                    break;
                case SubmoduleNode submoduleNode:
                    OpenSubmoduleCallback?.Invoke(submoduleNode.Submodule);
                    break;
                case WorktreeNode worktreeNode:
                    OpenWorktreeCallback?.Invoke(worktreeNode.Worktree);
                    break;
                case StashNode stashNode:
                    ApplyStashCallback?.Invoke(stashNode.Stash);
                    break;
            }
        }

        // 获取节点关联的 Reference
        private static Reference GetNodeReference(SidebarNode node)
        {
            return node switch
            {
                BranchNode branchNode => branchNode.Branch,
                RemoteBranchNode remoteBranchNode => remoteBranchNode.Branch,
                TagNode tagNode => tagNode.Tag,
                _ => null
            };
        }
    }

    // ===== spike 简化 POCO 节点类（对照 WPF SidebarItem 继承体系）=====
    //
    // 对照 WPF 继承体系（不迁移 INotifyPropertyChanged 复杂逻辑）：
    //   SidebarItem (abstract)           → SidebarNode (abstract)
    //   FolderSidebarItem                → GroupNode（分组/文件夹节点）
    //   SidebarGroupItem                 → GroupNode（带 GroupType）
    //   LocalBranchSidebarItem           → BranchNode
    //   RemoteBranchSidebarItem          → RemoteBranchNode
    //   TagSidebarItem                   → TagNode
    //   SubmoduleSidebarItem             → SubmoduleNode
    //   StashSidebarItem                 → StashNode
    //   WorktreeSidebarItem              → WorktreeNode
    //   MainWorktreeSidebarItem          → MainWorktreeNode
    //
    // XAML 绑定需要属性（非字段），因此 Name/Icon/Children 均为属性。
    // 图标用 emoji 字符串（对照 WPF PNG Image）。

    // 对照 WPF: SidebarItem（abstract 基类）
    public abstract class SidebarNode
    {
        // 节点显示名称（对照 WPF SidebarItem.Title）
        public string Name { get; set; }

        // 图标 emoji（对照 WPF Image Source PNG）
        public string Icon { get; set; }

        // 子节点集合（对照 WPF MultiselectionTreeViewItemCollection Children）
        public ObservableCollection<SidebarNode> Children { get; } = new ObservableCollection<SidebarNode>();

        // 展开状态（对照 WPF MultiselectionTreeViewItem.IsExpanded）
        public bool IsExpanded { get; set; }

        // 可见性（对照 WPF UIElement.Visibility，Avalonia 用 IsVisible）
        public bool IsVisible { get; set; } = true;

        // 是否文件夹节点（对照 WPF FolderSidebarItem 标识）
        public bool IsFolder { get; set; }
    }

    // 对照 WPF: SidebarGroupItem + FolderSidebarItem
    //   分组节点（Branches / Tags / Remotes / Stashes / Submodules / Worktrees / Pinned / 文件夹）
    public class GroupNode : SidebarNode
    {
        public GroupNode(string name, string icon)
        {
            Name = name;
            Icon = icon;
        }
    }

    // 对照 WPF: LocalBranchSidebarItem
    public class BranchNode : SidebarNode
    {
        public LocalBranch Branch { get; }

        public BranchNode(LocalBranch branch)
        {
            Branch = branch;
            Name = branch.Name;
            Icon = branch.IsActive ? "🌿*" : "🌿";
        }
    }

    // 对照 WPF: RemoteBranchSidebarItem
    public class RemoteBranchNode : SidebarNode
    {
        public RemoteBranch Branch { get; }

        public RemoteBranchNode(RemoteBranch branch)
        {
            Branch = branch;
            Name = branch.ShortName;
            Icon = "☁";
        }
    }

    // 对照 WPF: TagSidebarItem
    public class TagNode : SidebarNode
    {
        public Tag Tag { get; }

        public TagNode(Tag tag)
        {
            Tag = tag;
            Name = tag.Name;
            Icon = "🏷";
        }
    }

    // 对照 WPF: SubmoduleSidebarItem
    public class SubmoduleNode : SidebarNode
    {
        public Submodule Submodule { get; }

        public SubmoduleNode(Submodule submodule)
        {
            Submodule = submodule;
            Name = submodule.FriendlyName;
            Icon = "📦";
        }
    }

    // 对照 WPF: StashSidebarItem
    public class StashNode : SidebarNode
    {
        public StashRevision Stash { get; }

        public StashNode(StashRevision stash)
        {
            Stash = stash;
            Name = stash.Message;
            Icon = "📥";
        }
    }

    // 对照 WPF: WorktreeSidebarItem
    public class WorktreeNode : SidebarNode
    {
        public Worktree Worktree { get; }

        public WorktreeNode(Worktree worktree)
        {
            Worktree = worktree;
            Name = worktree.FriendlyName;
            Icon = worktree.IsActive ? "🗂*" : "🗂";
        }
    }

    // 对照 WPF: MainWorktreeSidebarItem
    public class MainWorktreeNode : SidebarNode
    {
        public Worktree Worktree { get; }

        public MainWorktreeNode(Worktree worktree)
        {
            Worktree = worktree;
            Name = worktree.FriendlyName;
            Icon = "🗂";
        }
    }
}
