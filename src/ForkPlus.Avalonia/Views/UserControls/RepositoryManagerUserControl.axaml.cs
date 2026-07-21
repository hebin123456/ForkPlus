using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RepositoryManagerUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RepositoryManagerUserControl.xaml.cs（594 行）：
    //   - 静态 Commands：RepositoryManagerUserControlCommands（OpenRepository/RenameRepository/
    //     RemoveRepository/RescanRepositories/OpenRepositoriesCommand）
    //   - 字段：JobQueue / _root / _recent / _repositories (RepositoryManagerTreeViewItem)
    //   - 属性：SelectedRepository / SelectedItems
    //   - 构造函数：初始化树结构 + ApplyLocalization + Refresh + Loaded 事件 +
    //     GridSplitter.DragCompleted + CommandBindings + ContextMenuOpening + NotificationCenter 订阅 +
    //     RescanUserRepositoriesCommand
    //   - Refresh(bool restoreSelection)：重建树 + 恢复展开/选中状态
    //   - CreateRecentRepositories / CreateRepositoryItems：构建仓库树
    //   - OnDrop：拖拽添加仓库
    //   - 多个事件处理：ContextMenuOpening / MouseDoubleClick / SelectionChanged
    //   - SelectRepositoryWithPath / SelectFirstRepository / SelectFirstRecent
    //   - CreateRepositoryContextMenuItems：右键菜单生成
    //
    // Avalonia 版差异（spike 简化策略）：
    //   - WPF MultiselectionTreeView → Avalonia TreeView（task spec 简化策略）
    //   - WPF RepositoryManagerTreeViewItem（继承 MultiselectionTreeViewItem）→ spike POCO
    //   - WPF RepositoryManager.Instance 单例 → spike 内部 ObservableCollection
    //   - WPF JobQueue → spike 不使用
    //   - WPF NotificationCenter 订阅 → spike 不接入
    //   - WPF DragDrop → spike 不实现
    //   - WPF CommandBindings → spike 用 Button.Click 替代
    //   - WPF ContextMenu → spike 顶部按钮 + 双击
    //   - WPF Image PNG → TextBlock emoji
    //   - WPF Dispatcher.Async → Dispatcher.UIThread.Post
    //   - WPF Visibility.Collapsed/Visible → IsVisible = false/true
    //   - WPF PreferencesLocalization → ServiceLocator.Localization
    //   - WPF MouseDoubleClick → DoubleTapped
    //
    // spike 简化（task spec 关键 API）：
    //   - task spec 关键 API：Initialize() / Refresh() / AddRepository(Repository) /
    //     RemoveRepository(Repository) / RepositorySelected 事件
    //   - task spec 简化：用 TreeView 显示仓库列表，RepositoryManagerTreeViewItem POCO
    //   - WPF Refresh(restoreSelection) → spike Refresh()（无 restoreSelection 参数）
    //   - 仓库操作（Open/Rename/Remove）→ 注入回调
    //   - git mm workspace 特殊处理 → spike 不实现
    //   - RescanRepositories → spike 不实现
    //   - 列宽持久化 → spike 不实现
    public partial class RepositoryManagerUserControl : UserControl
    {
        // ===== RepositoryManagerTreeViewItem POCO（task spec 简化策略）=====
        // 对照 WPF: RepositoryManagerTreeViewItem : MultiselectionTreeViewItem
        //   WPF 字段：Title / IsExpanded / IsInEditMode / Parent / Children
        // spike POCO：用 ObservableCollection<RepositoryManagerTreeViewItem> 替代 WPF Children
        public class RepositoryManagerTreeViewItem : INotifyPropertyChanged
        {
            private string _title;
            private bool _isExpanded;
            private string _iconEmoji = "📂";

            public string Title
            {
                get => _title;
                set { _title = value; OnPropertyChanged(nameof(Title)); }
            }

            public bool IsExpanded
            {
                get => _isExpanded;
                set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
            }

            // 对照 WPF: ImageSource RepositoryIcon → spike emoji（📁 仓库 / ⚠️ 警告）
            public string IconEmoji
            {
                get => _iconEmoji;
                set { _iconEmoji = value; OnPropertyChanged(nameof(IconEmoji)); }
            }

            // 对照 WPF: RepositoryManagerTreeViewItem Parent
            public RepositoryManagerTreeViewItem Parent { get; }

            // 对照 WPF: ObservableCollection<MultiselectionTreeViewItem> Children
            public ObservableCollection<RepositoryManagerTreeViewItem> Children { get; } = new ObservableCollection<RepositoryManagerTreeViewItem>();

            // spike 新增：关联的 Repository（仅 RepositoryItem 有值，section/folder 为 null）
            public ForkPlusSettings.RepositoryManagerSettings.Repository? Repository { get; set; }

            // spike 新增：是否为仓库节点（用于双击/选中判断）
            public bool IsRepository => Repository != null;

            public event PropertyChangedEventHandler PropertyChanged;

            public RepositoryManagerTreeViewItem() { }

            public RepositoryManagerTreeViewItem(string title, RepositoryManagerTreeViewItem parent = null)
            {
                _title = title;
                Parent = parent;
            }

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // ===== 私有字段（对照 WPF）=====
        // 对照 WPF: private readonly RepositoryManagerTreeViewItem _root / _recent / _repositories
        private readonly RepositoryManagerTreeViewItem _root;
        private readonly RepositoryManagerTreeViewItem _recent;
        private readonly RepositoryManagerTreeViewItem _repositories;

        // 对照 WPF: private readonly ObservableCollection<RepositoryManagerTreeViewItem>
        // spike 版：内部仓库列表（替代 RepositoryManager.Instance.Repositories 单例）
        private readonly List<ForkPlusSettings.RepositoryManagerSettings.Repository> _repositoryList = new List<ForkPlusSettings.RepositoryManagerSettings.Repository>();

        // ===== 事件（task spec 关键 API）=====
        // 对照 WPF: RepositoriesListBox_SelectionChanged → RepositoryDetailsUserControl.ShowDetails
        public event EventHandler<ForkPlusSettings.RepositoryManagerSettings.Repository?> RepositorySelected;

        // ===== 注入回调（替代 MainWindow.Instance / RepositoryManager.Instance 依赖）=====
        // 对照 WPF: Commands.OpenRepository.Execute(repository)
        public Action<ForkPlusSettings.RepositoryManagerSettings.Repository> OpenRepositoryCallback { get; set; }

        // 对照 WPF: Commands.RemoveRepository.Execute(this, selectedItems)
        public Action<ForkPlusSettings.RepositoryManagerSettings.Repository[]> RemoveRepositoriesCallback { get; set; }

        // 对照 WPF: MainWindow.Commands.OpenRepositoryInFileExplorer.Execute(path)
        public Action<string> OpenInFileExplorerCallback { get; set; }

        // 对照 WPF: Commands.RenameRepository.Execute(item)
        public Action<ForkPlusSettings.RepositoryManagerSettings.Repository> RenameRepositoryCallback { get; set; }

        // spike 新增：添加仓库回调（替代 WPF 拖拽 / AddRepositoryButton）
        public Action AddRepositoryCallback { get; set; }

        // ===== 构造函数 =====
        public RepositoryManagerUserControl()
        {
            InitializeComponent();

            // 对照 WPF: _root = new RepositoryManagerTreeViewItem(null) { Title = "" }
            _root = new RepositoryManagerTreeViewItem(string.Empty);
            // 对照 WPF: _recent = new RepositoryManagerSectionItem(_root, "Recent")
            _recent = new RepositoryManagerTreeViewItem("Recent", _root) { IconEmoji = "🕘" };
            // 对照 WPF: _repositories = new RepositoryManagerRepositorySectionItem(_root, "Repositories", this)
            _repositories = new RepositoryManagerTreeViewItem("Repositories", _root) { IconEmoji = "📚" };

            _root.IsExpanded = true;
            _recent.IsExpanded = true;
            _repositories.IsExpanded = true;
            _root.Children.Add(_recent);
            _root.Children.Add(_repositories);

            // 对照 WPF: RepositoriesTreeView.RootItem = _root
            RepositoriesTreeView.ItemsSource = _root.Children;

            ApplyLocalization();
            Refresh();
        }

        // ===== Initialize()（task spec 关键 API）=====
        // 对照 WPF: 构造函数已完成初始化（InitializeComponent + 树结构 + Refresh）
        // spike 版：task spec 关键 API，无参构造后已自动初始化，此方法允许外部触发重新初始化
        public void Initialize()
        {
            Refresh();
        }

        // ===== ApplyLocalization（对照 WPF）=====
        public void ApplyLocalization()
        {
            if (RepositoryManagerTitleTextBlock != null)
                RepositoryManagerTitleTextBlock.Text = Translate("Repository Manager");
            if (FallbackMessage != null)
                FallbackMessage.Text = Translate("Drop repository here to add");
            _recent.Title = Translate("Recent");
            _repositories.Title = Translate("Repositories");
        }

        // ===== Refresh()（task spec 关键 API）=====
        // 对照 WPF: public void Refresh(bool restoreSelection = true)
        //   WPF: ImportKnownGitMmWorkspaces + 重建树 + 恢复展开/选中状态
        // spike 版：重建 _recent + _repositories 子树（无 restoreSelection 参数）
        public void Refresh()
        {
            // 对照 WPF: if (RepositoryManager.Instance.Repositories.Length == 0) FallbackView.Show()
            if (_repositoryList.Count == 0)
            {
                if (FallbackPanel != null) FallbackPanel.IsVisible = true;
                if (RepositoriesTreeView != null) RepositoriesTreeView.IsVisible = false;
                return;
            }

            if (FallbackPanel != null) FallbackPanel.IsVisible = false;
            if (RepositoriesTreeView != null) RepositoriesTreeView.IsVisible = true;

            // 对照 WPF: _recent.Children.Clear() + _repositories.Children.Clear()
            _recent.Children.Clear();
            _repositories.Children.Clear();

            // 对照 WPF: CreateRecentRepositories(_recent)
            CreateRecentRepositories();

            // 对照 WPF: CreateRepositoryItems(_repositories)
            CreateRepositoryItems();
        }

        // 对照 WPF: private void CreateRecentRepositories(RepositoryManagerTreeViewItem root)
        //   WPF: 按 Opened 时间倒序取前 5 个
        // spike 版: 用 LastAccessTime 替代 Opened（ForkPlusSettings.Repository 没有 Opened 属性）
        private void CreateRecentRepositories()
        {
            var recent = _repositoryList
                .OrderByDescending(x => x.LastAccessTime)
                .Take(5)
                .ToArray();

            foreach (var repo in recent)
            {
                _recent.Children.Add(CreateRepositoryItem(repo, _recent));
            }
        }

        // 对照 WPF: private void CreateRepositoryItems(RepositoryManagerTreeViewItem root)
        //   WPF: 按 SourceDir 分组 + 路径文件夹结构
        // spike 版：扁平列表（不分组，简化策略）
        private void CreateRepositoryItems()
        {
            var sorted = _repositoryList
                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var repo in sorted)
            {
                _repositories.Children.Add(CreateRepositoryItem(repo, _repositories));
            }
        }

        // 对照 WPF: new RepositoryManagerRepositoryItem(repository, parent)
        //   WPF: base.Title = repository.Name()（extension method，WPF-only）
        // spike 版: repository.Name 属性（ForkPlusSettings.RepositoryManagerSettings.Repository）
        private RepositoryManagerTreeViewItem CreateRepositoryItem(
            ForkPlusSettings.RepositoryManagerSettings.Repository repository,
            RepositoryManagerTreeViewItem parent)
        {
            return new RepositoryManagerTreeViewItem(repository.Name ?? string.Empty, parent)
            {
                IconEmoji = "📁",
                Repository = repository
            };
        }

        // ===== AddRepository(Repository)（task spec 关键 API）=====
        // 对照 WPF: RepositoryManager.Instance.AddRepositories(paths) + Save + Refresh
        // spike 版：添加到内部列表 + Refresh
        public void AddRepository(ForkPlusSettings.RepositoryManagerSettings.Repository repository)
        {
            if (repository.Path == null) return;

            // 去重（对照 WPF: RepositoryManager.Instance 不会重复添加）
            if (_repositoryList.Any(x => x.Path == repository.Path)) return;

            _repositoryList.Add(repository);
            Refresh();
        }

        // ===== RemoveRepository(Repository)（task spec 关键 API）=====
        // 对照 WPF: Commands.RemoveRepository.Execute(this, selectedItems)
        //   WPF: RepositoryManager.Instance.DeleteRepositories(paths) + Save + Refresh
        // spike 版：从内部列表移除 + Refresh
        public void RemoveRepository(ForkPlusSettings.RepositoryManagerSettings.Repository repository)
        {
            if (repository.Path == null) return;

            int removed = _repositoryList.RemoveAll(x => x.Path == repository.Path);
            if (removed > 0)
            {
                Refresh();
            }
        }

        // ===== SetRepositories（spike 新增，批量设置仓库列表）=====
        // 对照 WPF: RepositoryManager.Instance.Repositories
        // spike 版：调用方通过此方法注入仓库列表（替代 RepositoryManager.Instance 单例）
        public void SetRepositories(IEnumerable<ForkPlusSettings.RepositoryManagerSettings.Repository> repositories)
        {
            _repositoryList.Clear();
            if (repositories != null)
            {
                _repositoryList.AddRange(repositories);
            }
            Refresh();
        }

        // ===== SelectRepositoryWithPath（对照 WPF）=====
        // 对照 WPF: public void SelectRepositoryWithPath(string path)
        public void SelectRepositoryWithPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var item = FindRepositoryItem(path, _root);
            if (item != null)
            {
                RepositoriesTreeView.SelectedItem = item;
            }
        }

        // 对照 WPF: SelectFirstRepository()
        public void SelectFirstRepository()
        {
            if (_repositories.Children.Count > 0)
            {
                RepositoriesTreeView.SelectedItem = _repositories.Children[0];
            }
        }

        // ===== 事件处理（对照 WPF）=====

        // 对照 WPF: RepositoriesListBox_SelectionChanged → RepositoryDetailsUserControl.ShowDetails
        //   WPF: RepositoryDetailsUserControl.ShowDetails(SelectedRepository?.Repository)
        private void RepositoriesTreeView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = RepositoriesTreeView.SelectedItem as RepositoryManagerTreeViewItem;
            if (selected != null && selected.IsRepository)
            {
                RepositorySelected?.Invoke(this, selected.Repository);
            }
            e.Handled = true;
        }

        // 对照 WPF: RepositoriesListBox_MouseDoubleClick → Commands.OpenRepository.Execute
        //   WPF: if (lastClickedItem is RepositoryManagerRepositoryItem) Commands.OpenRepository.Execute(repo)
        // spike 版: 双击仓库节点打开仓库
        private void RepositoriesTreeView_DoubleTapped(object sender, RoutedEventArgs e)
        {
            var selected = RepositoriesTreeView.SelectedItem as RepositoryManagerTreeViewItem;
            if (selected != null && selected.IsRepository && selected.Repository != null)
            {
                OpenRepositoryCallback?.Invoke(selected.Repository);
            }
        }

        // spike 新增：AddRepositoryButton 点击
        // 对照 WPF: 拖拽添加仓库 / Add Repository 按钮
        private void AddRepositoryButton_Click(object sender, RoutedEventArgs e)
        {
            AddRepositoryCallback?.Invoke();
        }

        // spike 新增：RefreshButton 点击
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        // ===== 私有辅助方法 =====

        // 对照 WPF: private bool SelectRepositoryItemWithPath(string path, MultiselectionTreeViewItem parent)
        private RepositoryManagerTreeViewItem FindRepositoryItem(string path, RepositoryManagerTreeViewItem parent)
        {
            foreach (var child in parent.Children)
            {
                if (child.IsRepository && child.Repository != null && child.Repository.Path == path)
                {
                    return child;
                }
                var found = FindRepositoryItem(path, child);
                if (found != null) return found;
            }
            return null;
        }

        // 对照 WPF: private static string Translate(string text)
        //   WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        //   spike: ServiceLocator.Localization.Translate(text, lang)
        private static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (ServiceLocator.Localization == null) return text;
            try
            {
                return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
            }
            catch
            {
                return text;
            }
        }
    }
}
