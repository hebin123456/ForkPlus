using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 SearchTabItem（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/SearchTabItem.xaml.cs（368 行）：
    //   - TabItem + ILocalizableControl
    //   - 字段：_isSearchInProgress / _searchType / _searchScope / _root
    //   - 属性：SearchQuery / RepositoryUserControl / IsSearchInProgress
    //   - 事件：SearchQueryChanged
    //   - Initialize(RepositoryUserControl) / ApplyLocalization / OnActivated
    //   - ClearMatches / AddMatch(RevisionWithFiles)
    //   - TreeView_SelectionChanged / SearchDropdownButtonContextMenu_Opened /
    //     FilterTextBox_DropdownContextMenuOpened / FilterTextBox_FilterRequestChanged /
    //     FilterTextBox_ClearButtonClicked
    //   - RefreshBusyIndicator / RefreshResultCount / GetQueryTypeName
    //   - CreateSearchDropdownMenuItems / RefreshSearchControls / RemoveSelectedMatch
    //   - AddSearchQueryToRecent / SearchMenuItemHeader / SearchPlaceholder / Translate
    //
    // Avalonia 版差异（spike 简化策略）：
    //   - WPF TabItem 基类 → Avalonia UserControl
    //   - WPF MultiselectionTreeView → Avalonia ListBox
    //   - WPF FilterTextBox 自定义控件 → Avalonia TextBox
    //   - WPF SearchDropdownButton + ContextMenu → Avalonia ComboBox
    //   - WPF Image PNG → TextBlock emoji
    //   - WPF BusyIndicator.Show/Collapse → ProgressBar.IsVisible = true/false
    //   - WPF Dispatcher.Async → Dispatcher.UIThread.Post
    //   - WPF Visibility.Collapsed/Visible → IsVisible = false/true
    //   - WPF PreferencesLocalization → ServiceLocator.Localization
    //
    // spike 简化（task spec 关键 API）：
    //   - task spec 关键 API：Initialize(RepositoryUserControl) / Search(string) / Clear()
    //   - WPF AddMatch(RevisionWithFiles) → spike AddMatch(SearchResult) POCO
    //   - WPF SearchQueryChanged 事件 → spike Search(string) 直接调用
    //   - WPF 4 种搜索类型 → spike ComboBox
    //   - WPF 2 种 scope → spike 不迁移
    //   - WPF RemoveSelectedMatch → spike RemoveSelected 方法
    //   - WPF AddSearchQueryToRecent → spike 不实现
    //   - JobQueue → spike 不使用
    public partial class SearchTabItem : UserControl
    {
        // ===== 内部 POCO ViewModel（对照 WPF SidebarSearchItem / RevisionWithFiles）=====
        public class SearchResult : INotifyPropertyChanged
        {
            public string Sha { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public string AuthorEmail { get; set; } = string.Empty;
            public DateTime AuthorDate { get; set; }
            public string FilePath { get; set; }

            // spike 绑定辅助属性
            public string AuthorLine => "@" + (Author ?? string.Empty) + "  ·  " + AuthorDate.ToString("yyyy-MM-dd");
            public string RelativeDate => AuthorDate.ToString("yyyy-MM-dd HH:mm");

            public event PropertyChangedEventHandler PropertyChanged;
        }

        // ===== 私有字段（对照 WPF）=====
        // 对照 WPF: private bool _isSearchInProgress
        private bool _isSearchInProgress;

        // 对照 WPF: private RevisionSearchType _searchType
        private string _searchType = "Commit Message";

        // 对照 WPF: MultiselectionTreeViewItem _root
        private readonly ObservableCollection<SearchResult> _results = new ObservableCollection<SearchResult>();

        // 对照 WPF: public RepositoryUserControl RepositoryUserControl { get; private set; }
        // spike 版：父控件引用（spike 用 object 占位，对照 spike 策略 #1）
        public object RepositoryUserControl { get; private set; }

        // ===== 事件（对照 WPF: public event EventHandler SearchQueryChanged）=====
        public event EventHandler<string> SearchQueryChanged;

        // 对照 WPF: public bool IsSearchInProgress
        public bool IsSearchInProgress
        {
            get => _isSearchInProgress;
            set
            {
                _isSearchInProgress = value;
                RefreshBusyIndicator();
            }
        }

        // ===== 注入回调（替代 RepositoryUserControl.SelectRevisions 依赖）=====
        // 对照 WPF: TreeView_SelectionChanged → RepositoryUserControl.SelectRevisions(shaArray)
        public Action<string[]> SelectRevisionsCallback { get; set; }

        // 对照 WPF: SearchButton_Click → 实际搜索由 RepositoryUserControl 触发
        public Action<string, string> SearchCallback { get; set; }

        // ===== 构造函数（task spec spike 签名）=====
        public SearchTabItem()
        {
            InitializeComponent();
            // 对照 WPF: TreeView.RootItem = _root
            SearchResultsListBox.ItemsSource = _results;
            RefreshResultCount();
        }

        // ===== Initialize(RepositoryUserControl)（task spec 关键 API）=====
        // 对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl)
        //   WPF: RepositoryUserControl = repositoryUserControl;
        // spike 版：task spec 关键 API，注入父控件（spike 用 object 占位）
        public void Initialize(object repositoryUserControl)
        {
            RepositoryUserControl = repositoryUserControl;
        }

        // ===== Search(string)（task spec 关键 API）=====
        // 对照 WPF: FilterTextBox.KeyDown (Key.Return) → SearchQueryChanged event +
        //   AddSearchQueryToRecent(SearchQuery)
        // spike 版：task spec 关键 API，触发搜索 + 显示 loading
        public void Search(string searchString)
        {
            string query = searchString ?? string.Empty;
            IsSearchInProgress = true;

            // 对照 WPF: this.SearchQueryChanged?.Invoke(this, EventArgs.Empty)
            SearchQueryChanged?.Invoke(this, query);

            // spike 版：调用注入的 SearchCallback（实际搜索由 RepositoryUserControl 处理）
            // 真实搜索逻辑留待后续 Phase
            Dispatcher.UIThread.Post(() =>
            {
                // spike 版不实际搜索，等待外部 AddMatch 注入结果
                if (_results.Count == 0)
                {
                    IsSearchInProgress = false;
                    FoundCommitsTextBlock.Text = Translate("No results");
                }
            });
        }

        // ===== Clear()（task spec 关键 API）=====
        // 对照 WPF: public void ClearMatches()
        //   WPF: TreeView.SelectedItems.Clear() + _root.Children.Clear() + FoundCommitsTextBlock.Text = ""
        public void Clear()
        {
            SearchResultsListBox.SelectedItem = null;
            _results.Clear();
            if (FilterTextBox != null) FilterTextBox.Text = string.Empty;
            RefreshResultCount();
        }

        // ===== AddMatch（spike 新增，对照 WPF AddMatch(RevisionWithFiles)）=====
        // 对照 WPF: public void AddMatch(RevisionWithFiles match)
        //   WPF: _root.Children.Add(new SidebarSearchItem(match, searchString))
        // spike 版：用 SearchResult POCO 替代 RevisionWithFiles
        public void AddMatch(SearchResult match)
        {
            if (match == null) return;
            _results.Add(match);
            RefreshResultCount();
            IsSearchInProgress = false;
        }

        // ===== SetResults（spike 新增，批量设置搜索结果）=====
        public void SetResults(IEnumerable<SearchResult> results)
        {
            _results.Clear();
            if (results != null)
            {
                foreach (var r in results)
                {
                    _results.Add(r);
                }
            }
            RefreshResultCount();
            IsSearchInProgress = false;
        }

        // ===== 事件处理（对照 WPF）=====

        // 对照 WPF: FilterTextBox.KeyDown (Key.Return) → SearchQueryChanged + AddSearchQueryToRecent
        private void FilterTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                Search(FilterTextBox?.Text ?? string.Empty);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // 对照 WPF: Key.Escape → SearchQueryChanged + SidebarActivateRepositoryTab
                Clear();
                e.Handled = true;
            }
        }

        // 对照 WPF: SearchButton_Click (spike 新增，WPF 用 Enter 触发)
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            Search(FilterTextBox?.Text ?? string.Empty);
        }

        // 对照 WPF: FilterTextBox_ClearButtonClicked → SearchQueryChanged
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        // 对照 WPF: TreeView_SelectionChanged → RepositoryUserControl.SelectRevisions
        //   WPF: RepositoryUserControl.SelectRevisions(array.Map(x => x.Sha))
        // spike 版: 调用注入的 SelectRevisionsCallback
        private void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is SearchResult result)
            {
                SelectRevisionsCallback?.Invoke(new[] { result.Sha });
            }
        }

        // spike 新增：双击打开 commit（对照 WPF TreeView 双击行为）
        private void SearchResultsListBox_DoubleTapped(object sender, RoutedEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is SearchResult result)
            {
                SelectRevisionsCallback?.Invoke(new[] { result.Sha });
            }
        }

        // 对照 WPF: RefreshSearchControls → SearchDropdownButton.Content = SearchMenuItemHeader(_searchType)
        private void SearchTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchTypeComboBox?.SelectedItem is ComboBoxItem item)
            {
                _searchType = item.Content as string ?? "Commit Message";
                // spike 版：更新 FilterTextBox Watermark（对照 WPF FilterTextBox.Placeholder）
                if (FilterTextBox != null)
                {
                    FilterTextBox.Watermark = SearchPlaceholder(_searchType);
                }
            }
        }

        // ===== 私有辅助方法 =====

        // 对照 WPF: private void RefreshBusyIndicator()
        //   WPF: if (IsSearchInProgress) BusyIndicator.Show() else BusyIndicator.Collapse()
        private void RefreshBusyIndicator()
        {
            if (BusyIndicator != null)
            {
                BusyIndicator.IsVisible = IsSearchInProgress;
            }
        }

        // 对照 WPF: private void RefreshResultCount()
        //   WPF: if (_root.Children.Count >= 1000) ">{0} results" else "{0} results"
        private void RefreshResultCount()
        {
            if (FoundCommitsTextBlock == null) return;
            if (_results.Count >= 1000)
            {
                FoundCommitsTextBlock.Text = string.Format(Translate(">{0} results"), 1000);
            }
            else
            {
                FoundCommitsTextBlock.Text = string.Format(Translate("{0} results"), _results.Count);
            }
        }

        // 对照 WPF: private static string SearchPlaceholder(RevisionSearchType searchType)
        private static string SearchPlaceholder(string searchType)
        {
            switch (searchType)
            {
                case "Commit Message": return Translate("Commit message");
                case "Author": return Translate("Author or email");
                case "Path": return Translate("Path, e.g. 'src/*.js'");
                case "Diff Content": return Translate("Source code");
                default: return Translate("Search commits...");
            }
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
