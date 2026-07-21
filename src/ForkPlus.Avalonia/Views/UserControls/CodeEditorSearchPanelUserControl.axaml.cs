using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Search;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 CodeEditorSearchPanelUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/CodeEditorSearchPanelUserControl.xaml.cs（313 行）：
    //   - SearchResultBackgroundRenderer（IBackgroundRenderer，高亮搜索结果）
    //   - SearchResult（TextSegment，搜索匹配区间）
    //   - Attach(TextArea) / ShowSearchBar / HideSearchBar / FindNext / FindPrevious
    //   - DoSearch（遍历文档全文搜索，IgnoreCase）
    //   - SelectResult（选中 + 滚动到可视区）
    //   - SlidingPanelHelper + TranslateTransform（滑入/滑出动画）
    //   - SearchTextBox.PreviewKeyDown：Return/F3 → FindNext/Previous
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF AvalonEdit.TextEditor → AvaloniaEdit.TextEditor（包已引用）
    //   - WPF IBackgroundRenderer 自定义高亮 → AvaloniaEdit SearchPanel 内置高亮
    //   - WPF SlidingPanelHelper + TranslateTransform → IsVisible 切换（spike 简化）
    //   - WPF PreviewKeyDown → KeyDown（Avalonia API）
    //   - WPF KeyboardHelper.IsShiftDown → KeyModifiers.Shift（Avalonia API）
    //   - task spec 关键 API：Initialize(TextEditor) / Show() / Hide() / Search(string) / Replace(string, string)
    //   - spike 简化：使用 AvaloniaEdit.TextEditor 的 SearchReplacePanel API
    //
    // spike 简化：
    //   - Initialize(TextEditor editor) 方法（task spec 关键 API）
    //   - Show() / Hide() 方法（task spec 关键 API）
    //   - Search(string) 方法（task spec 关键 API）
    //   - Replace(string, string) 方法（task spec 关键 API）
    //   - 用 AvaloniaEdit.Search.SearchPanel 内置 API 替代自定义高亮渲染器
    //   - IsVisible 切换替代滑入/滑出动画
    public partial class CodeEditorSearchPanelUserControl : UserControl
    {
        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        private TextEditor _editor; // 对照 WPF: _textArea（spike 直接持有 TextEditor）
        private SearchPanel _searchPanel; // AvaloniaEdit 内置 SearchPanel

        // ===== 公共属性（对照 WPF）=====
        // 搜索请求（对照 WPF: SearchRequest => SearchTextBox.Text）
        public string SearchRequest => SearchTextBox?.Text ?? string.Empty;

        // 是否聚焦（对照 WPF: IsTextBoxFocused）
        public bool IsTextBoxFocused => SearchTextBox?.IsFocused ?? false;

        // 面板高度（对照 WPF: PanelHeight）
        public double PanelHeight => IsSearchBarVisible ? 30.0 : 0.0;

        // 是否可见（对照 WPF: _isSearchBarVisible）
        public bool IsSearchBarVisible { get; private set; }

        // ===== 构造函数（spike 用 IServiceProvider）=====
        public CodeEditorSearchPanelUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
        }

        // ===== Initialize(TextEditor)（task spec 关键 API）=====
        // 对照 WPF: Attach(TextArea textArea) — 绑定编辑器 + 安装高亮渲染器
        // spike 版：Initialize(TextEditor) + 用 AvaloniaEdit SearchPanel.Install 替代自定义渲染器
        public void Initialize(TextEditor editor)
        {
            _editor = editor;
            if (_editor != null)
            {
                // 对照 WPF: _renderer = new SearchResultBackgroundRenderer()
                // spike 版：用 AvaloniaEdit 内置 SearchPanel（已含高亮 + FindNext/Previous）
                // 注意：CodeEditor 基类构造函数已调 SearchPanel.Install，这里获取引用即可
                // 但独立 TextEditor 需自行 Install
                _searchPanel = SearchPanel.Install(_editor);
            }
        }

        // ===== Show()（task spec 关键 API，对照 WPF: ShowSearchBar）=====
        public new void Show()
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

        // ===== Hide()（task spec 关键 API，对照 WPF: HideSearchBar）=====
        public new void Hide()
        {
            if (IsSearchBarVisible)
            {
                if (SearchPanelContainer != null)
                {
                    SearchPanelContainer.IsVisible = false;
                }
                IsSearchBarVisible = false;
                // 对照 WPF: _renderer.CurrentResults.Clear()
                // spike 版：AvaloniaEdit SearchPanel.Close 自带清理
                _searchPanel?.Close();
            }
        }

        // ===== Search(string)（task spec 关键 API，对照 WPF: DoSearch）=====
        // WPF: 遍历文档全文搜索（IgnoreCase）+ 高亮所有匹配
        // spike 版：设置 SearchPanel 搜索词（内置高亮 + 匹配计数）
        public void Search(string query)
        {
            if (SearchTextBox != null)
            {
                SearchTextBox.Text = query ?? string.Empty;
            }
            DoSearch();
        }

        // ===== Replace(string, string)（task spec 关键 API）=====
        // WPF 版无替换功能，spike 新增（task spec 要求）
        // 用 AvaloniaEdit SearchPanel 替换当前匹配
        public void Replace(string search, string replace)
        {
            if (_editor == null) return;
            // spike 版：简单文本替换（遍历文档替换所有匹配）
            if (string.IsNullOrEmpty(search)) return;
            string text = _editor.Document.Text;
            string newText = text.Replace(search, replace ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            _editor.Document.Text = newText;
        }

        // ===== FindNext()（对照 WPF）=====
        public void FindNext()
        {
            // 对照 WPF: 遍历 _renderer.CurrentResults 找下一个
            // spike 版：AvaloniaEdit SearchPanel 内置 FindNext
            _searchPanel?.FindNext();
        }

        // ===== FindPrevious()（对照 WPF）=====
        public void FindPrevious()
        {
            // 对照 WPF: 遍历 _renderer.CurrentResults 找上一个
            // spike 版：AvaloniaEdit SearchPanel 内置 FindPrevious
            _searchPanel?.FindPrevious();
        }

        // ===== DoSearch（对照 WPF，spike 委托给 AvaloniaEdit SearchPanel）=====
        // WPF: 遍历文档全文搜索 + _renderer.CurrentResults.Add
        // spike: 更新 SearchPanel.SearchPattern（内置高亮 + 匹配）
        private void DoSearch()
        {
            if (_searchPanel == null) return;
            // 设置搜索模式（AvaloniaEdit SearchPanel 自动高亮所有匹配）
            _searchPanel.SearchPattern = SearchRequest;
        }

        // ===== SearchTextBox_TextChanged（对照 WPF）=====
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            DoSearch();
        }

        // ===== SearchTextBox_KeyDown（对照 WPF: PreviewKeyDown）=====
        // WPF: Return/F3 → FindNext/Previous（Shift 区分方向）
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return || e.Key == Key.F3)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    FindPrevious();
                }
                else
                {
                    FindNext();
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
            FindNext();
        }

        private void JumpToPreviousSearchResultButton_Click(object sender, RoutedEventArgs e)
        {
            FindPrevious();
        }

        // ===== ReplaceButton_Click（spike 新增）=====
        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            Replace(SearchRequest, ReplaceTextBox?.Text ?? string.Empty);
        }
    }
}
