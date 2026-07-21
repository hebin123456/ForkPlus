using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RevisionSearchPanelUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RevisionSearchPanelUserControl.xaml.cs（162 行）：
    //   - SearchPanelPlaceholder DependencyProperty（Grid，滑入/滑出容器）
    //   - SearchString 属性（SearchTextBox.Text.Trim()）
    //   - IsTextBoxFocused / IsBusyIndicatorVisible / IsSearchBarVisible 属性
    //   - 4 个事件：SearchQueryChanged / JumpToPreviousSearchResult / JumpToNextSearchResult / Closed
    //   - ShowSearchBar / HideSearchBar / UpdateMatchesCount 方法
    //   - SlidingPanelHelper.ShowPanel/HidePanel（TranslateTransform 动画）
    //   - SearchTextBox.PreviewKeyDown：Return/F3 → FindNext/Previous（Shift 区分方向）
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF DependencyProperty SearchPanelPlaceholder → 直接 Grid 控件
    //   - WPF SlidingPanelHelper + TranslateTransform 动画 → IsVisible 切换（spike 简化）
    //   - WPF PreviewKeyDown → KeyDown（Avalonia API）
    //   - WPF KeyboardHelper.IsShiftDown → KeyModifiers.Shift（Avalonia API）
    //   - WPF TextChangedEventArgs → Avalonia TextChangedEventArgs（API 一致）
    //   - task spec 关键 API：Initialize(RepositoryUserControl) / Search(string) / Clear() / SearchChanged
    //
    // spike 简化：
    //   - Initialize(object repositoryUserControl) 方法（task spec 关键 API）
    //   - Search(string) 方法（task spec 关键 API）
    //   - Clear() 方法（task spec 关键 API）
    //   - SearchChanged 事件（task spec 关键 API，对照 WPF SearchQueryChanged）
    //   - IsVisible 切换替代滑入/滑出动画
    //   - Return/F3 → JumpToNext/Previous（保留键盘快捷键）
    public partial class RevisionSearchPanelUserControl : UserControl
    {
        // ===== 公共事件 =====
        // 对照 WPF: SearchQueryChanged（task spec 重命名为 SearchChanged）
        public event EventHandler SearchChanged;

        // 对照 WPF: JumpToPreviousSearchResult / JumpToNextSearchResult / Closed
        public event EventHandler JumpToPreviousSearchResult;
        public event EventHandler JumpToNextSearchResult;
        public event EventHandler Closed;

        // ===== 公共属性（对照 WPF）=====
        // 搜索字符串（对照 WPF: SearchString => SearchTextBox.Text.Trim()）
        public string SearchString => SearchTextBox?.Text?.Trim() ?? string.Empty;

        // 是否聚焦（对照 WPF: IsTextBoxFocused）
        public bool IsTextBoxFocused => SearchTextBox?.IsFocused ?? false;

        // 是否可见（对照 WPF: IsSearchBarVisible）
        public bool IsSearchBarVisible { get; private set; }

        // loading 指示器（对照 WPF: IsBusyIndicatorVisible）
        public bool IsBusyIndicatorVisible
        {
            get => BusyIndicator?.IsVisible ?? false;
            set
            {
                if (BusyIndicator != null)
                {
                    BusyIndicator.IsVisible = value;
                }
            }
        }

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        // spike 版用 object 占位（对照 WPF: RepositoryUserControl）
        public object RepositoryUserControl { get; private set; }

        // ===== 构造函数（spike 用 IServiceProvider）=====
        public RevisionSearchPanelUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
        }

        // ===== Initialize(object)（task spec 关键 API）=====
        // 对照 WPF: 无独立 Initialize，依赖通过 DataContext 注入
        // spike 版：Initialize 方法注入 RepositoryUserControl
        public void Initialize(object repositoryUserControl)
        {
            RepositoryUserControl = repositoryUserControl;
        }

        // ===== Search(string)（task spec 关键 API）=====
        // 设置搜索文本并触发搜索（对照 WPF: SearchTextBox.Text 设置 + TextChanged）
        public void Search(string query)
        {
            if (SearchTextBox != null)
            {
                SearchTextBox.Text = query ?? string.Empty;
            }
            // TextChanged 事件会自动触发 SearchChanged
            if (!IsSearchBarVisible)
            {
                Show();
            }
        }

        // ===== Clear()（task spec 关键 API）=====
        // 清空搜索（对照 WPF: HideSearchBar + SearchTextBox.Clear()）
        public void Clear()
        {
            if (SearchTextBox != null)
            {
                SearchTextBox.Text = string.Empty;
            }
            if (MatchesTextBlock != null)
            {
                MatchesTextBlock.Text = string.Empty;
            }
        }

        // ===== Show()（对照 WPF: ShowSearchBar）=====
        public void Show()
        {
            if (SearchPanelContainer != null)
            {
                SearchPanelContainer.IsVisible = true;
            }
            IsSearchBarVisible = true;
            if (SearchTextBox != null)
            {
                SearchTextBox.SelectAll();
                SearchTextBox.Focus();
            }
        }

        // ===== Hide()（对照 WPF: HideSearchBar）=====
        public void Hide()
        {
            if (IsSearchBarVisible)
            {
                if (SearchPanelContainer != null)
                {
                    SearchPanelContainer.IsVisible = false;
                }
                Closed?.Invoke(this, EventArgs.Empty);
                IsSearchBarVisible = false;
            }
        }

        // ===== UpdateMatchesCount(int?)（对照 WPF）=====
        public void UpdateMatchesCount(int? matches)
        {
            if (MatchesTextBlock == null) return;
            if (matches.HasValue)
            {
                int value = matches.GetValueOrDefault();
                MatchesTextBlock.Text = value == 1 ? $"{value} match" : $"{value} matches";
            }
            else
            {
                MatchesTextBlock.Text = string.Empty;
            }
        }

        // ===== SearchTextBox_TextChanged（对照 WPF）=====
        // WPF: TextChangedEventArgs → Avalonia TextChangedEventArgs（API 一致）
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchChanged?.Invoke(this, EventArgs.Empty);
        }

        // ===== SearchTextBox_KeyDown（对照 WPF: PreviewKeyDown）=====
        // WPF: Return/F3 → FindNext/Previous（Shift 区分方向）
        // Avalonia: KeyDown + KeyModifiers.Shift
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.F3)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    JumpToPreviousSearchResult?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    JumpToNextSearchResult?.Invoke(this, EventArgs.Empty);
                }
                e.Handled = true;
            }
        }

        // ===== 按钮事件（对照 WPF）=====
        private void CloseSearchContainerButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void JumpToNextSearchResultButton_Click(object sender, RoutedEventArgs e)
        {
            JumpToNextSearchResult?.Invoke(this, EventArgs.Empty);
        }

        private void JumpToPreviousSearchResultButton_Click(object sender, RoutedEventArgs e)
        {
            JumpToPreviousSearchResult?.Invoke(this, EventArgs.Empty);
        }
    }
}
