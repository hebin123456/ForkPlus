using System;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.Search;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // Phase 2.6：Avalonia 版 CodeEditor 基类（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/CodeEditor.cs（94 行）：
    //   - WPF CodeEditor : ICSharpCode.AvalonEdit.TextEditor
    //   - 构造函数设置 Options（InheritWordWrapIndentation / EnableHyperlinks / EnableEmailHyperlinks）
    //   - TextArea.SelectionBorder / SelectionCornerRadius 调整（WPF-only，AvaloniaEdit 用 SelectionBrush）
    //   - TextArea.TextView.BackgroundRenderers.Add(ClearTypeBackgroundRenderer)（WPF ClearType 渲染优化）
    //   - RenderOptions.SetClearTypeHint（WPF-only ClearType 字体渲染提示）
    //   - OnApplyTemplate: 获取 PART_SearchPanelUserControl TemplatePart（WPF 自定义搜索面板）
    //   - 公共：ShowSearchBar / HideSearchBar / IsSearchBarFocused / SearchBarHeight
    //   - GetScrollPosition / SetScrollPosition
    //   - OnPreviewKeyDown: F3/Ctrl+F → ShowSearchBar; Escape → HideSearchBar;
    //     DiffCodeEditor 子类的 Ctrl+Shift+C → CopyAsPatchCommand（Phase 3.9b 接入）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 继承 AvaloniaEdit.TextEditor（AvaloniaEdit 12.0 + Avalonia 11.3.18，命名空间 AvaloniaEdit）
    //   2. 跳过 ClearType 相关（WPF-only，Avalonia 跨平台用默认渲染）
    //   3. 跳过 TextArea.SelectionBorder / SelectionCornerRadius（WPF-only，
    //      AvaloniaEdit 用 SelectionBrush/SelectionForeground，spike 不调整）
    //   4. 搜索面板用 AvaloniaEdit 内置 SearchPanel.Install(this)，
    //      替代 WPF 自定义 PART_SearchPanelUserControl TemplatePart
    //   5. OnPreviewKeyDown → OnKeyDown（Avalonia 没有 WPF 的 Preview tunneling 事件，
    //      用 KeyEventArgs.Handled + bubble routing 替代）
    //   6. F3 / Ctrl+F / Escape 已由 AvaloniaEdit 的 SearchInputHandler 内置处理，
    //      这里仅保留 F3 显式 ShowSearchBar 兜底（与 WPF 行为对齐）
    //
    // 本 spike 版暂不迁移（留 Phase 3.9b）：
    //   - ClearTypeBackgroundRenderer（WPF ClearType 优化，Avalonia 不需要）
    //   - CodeEditorSearchPanelUserControl（WPF 自定义搜索面板，Avalonia 用 AvaloniaEdit 内置）
    //   - CopyAsPatchCommand（DiffCodeEditor 子类的 Ctrl+Shift+C 快捷键）
    //   - IsSearchBarFocused / SearchBarHeight 真实实现（spike 返回 false/0.0）
    public class CodeEditor : TextEditor
    {
        private readonly SearchPanel _searchPanel;

        // 对照 WPF: IsSearchBarFocused => _templatePartSearchPanel?.IsTextBoxFocused ?? false
        // spike 版返回 false（AvaloniaEdit SearchPanel 内部状态不暴露 IsClosed 公共 API）
        public bool IsSearchBarFocused => false;

        // 对照 WPF: SearchBarHeight => _templatePartSearchPanel?.PanelHeight ?? 0.0
        // spike 版返回 0.0（AvaloniaEdit 内置 SearchPanel 不暴露高度 API）
        public double SearchBarHeight => 0.0;

        public CodeEditor()
        {
            // 对照 WPF: base.Options 配置
            Options.InheritWordWrapIndentation = false;
            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;

            // 安装 AvaloniaEdit 内置搜索面板（替代 WPF PART_SearchPanelUserControl TemplatePart）。
            // SearchPanel.Install 会自动注册到 TextArea，默认快捷键 Ctrl+F / Ctrl+H / Escape。
            _searchPanel = SearchPanel.Install(this);
        }

        // 对照 WPF: public void ShowSearchBar() => _templatePartSearchPanel?.ShowSearchBar()
        public void ShowSearchBar()
        {
            _searchPanel?.Open();
        }

        // 对照 WPF: public void HideSearchBar() => _templatePartSearchPanel?.HideSearchBar()
        public void HideSearchBar()
        {
            _searchPanel?.Close();
        }

        // 对照 WPF: public double GetScrollPosition() => base.TextArea.TextView.VerticalOffset
        public double GetScrollPosition()
        {
            return TextArea.TextView.VerticalOffset;
        }

        // 对照 WPF: public void SetScrollPosition(double y) => ScrollToVerticalOffset(y)
        public void SetScrollPosition(double y)
        {
            ScrollToVerticalOffset(y);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // 对照 WPF OnPreviewKeyDown：
            //   - F3 / Ctrl+F → ShowSearchBar（AvaloniaEdit SearchInputHandler 已内置处理）
            //   - Escape → HideSearchBar（AvaloniaEdit SearchInputHandler 已内置处理）
            //   - DiffCodeEditor Ctrl+Shift+C → CopyAsPatchCommand（Phase 3.9b 在子类覆盖）
            //
            // spike 版不在 CodeEditor 基类覆盖快捷键，让 AvaloniaEdit 默认行为生效。
            // Phase 3.9b 会在 DiffCodeEditor 子类覆盖此方法添加 CopyAsPatchCommand 快捷键。
            base.OnKeyDown(e);
        }
    }
}
