using ForkPlus.Git.Diff.Presentation;

namespace ForkPlus.Avalonia.Controls.Editor.Diff
{
    // Phase 2.6：Avalonia 版 DiffCodeEditor（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Diff/DiffCodeEditor.cs（233 行）：
    //   - 继承 CodeEditor（WPF CodeEditor : ICSharpCode.AvalonEdit.TextEditor）
    //   - 字段：_visualPatch / _backgroundColorizer / _textColorizer /
    //     _syntaxHighlighting / _diffLineNumberMargin
    //   - 构造函数：SetResourceReference(StyleProperty) + 创建 4 个辅助对象 +
    //     BackgroundRenderers.Add / LineTransformers.Add / LeftMargins.Add +
    //     WeakEventManager 订阅 NotificationCenter.ApplicationThemeChanged /
    //     DisableSyntaxHighlightingChanged
    //   - VisualPatch setter：更新 line numbers / highlighting source / 调 RefreshSyntaxHighlighting /
    //     RefreshScrollbarMap / InvalidateVisual
    //   - RefreshScrollbarMap：用 StreamGeometry 画 src/dst diff map（WPF-only Path）
    //   - OnRenderSizeChanged：调 RefreshScrollbarMap
    //   - CreateBackgroundHighlightingSource：根据 HighlightingScheme 生成 5 种 HighlightingSource 数组
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 继承 Avalonia 版 CodeEditor（src/ForkPlus.Avalonia/Controls/Editor/CodeEditor.cs）
    //   2. 跳过 _backgroundColorizer / _textColorizer / _syntaxHighlighting /
    //      _diffLineNumberMargin（Phase 3.9b 迁移 DiffBackgroundColorizer / DiffTextColorizer /
    //      SyntaxHighlighting / DiffLineNumberMargin）
    //   3. 跳过 NotificationCenter 订阅（NotificationCenter 在 WPF 工程，Avalonia 工程不可访问，
    //      Phase 0/4 抽象 INotificationService 后再接入）
    //   4. 跳过 RefreshScrollbarMap（WPF StreamGeometry + Path TemplatePart，Phase 3.9b 用
    //      Avalonia StreamGeometry + Polyline 重新实现）
    //   5. VisualPatch setter 简化为：仅设置 base.Text = patch 字符串
    //
    // 本 spike 版暂不迁移（留 Phase 3.9b）：
    //   - DiffBackgroundColorizer / DiffTextColorizer（diff 行背景色 / 文字色着色器）
    //   - SyntaxHighlighting（语法高亮，依赖 AvaloniaEdit.TextMate 0.10.12）
    //   - DiffLineNumberMargin（自定义行号边距，显示 src/dst 双行号）
    //   - RefreshScrollbarMap（滚动条 diff 缩略图）
    //   - NotificationCenter 事件订阅（ApplicationThemeChanged / DisableSyntaxHighlightingChanged）
    //   - CreateBackgroundHighlightingSource 5 种 HighlightingType（Add/Remove/Alignment/ExactAdd/ExactRemove）
    //
    // 本 spike 版验证：
    //   - CodeEditor 基类继承链可工作（AvaloniaEdit.TextEditor → CodeEditor → DiffCodeEditor）
    //   - VisualPatch.StringValue 可作为 base.Text 渲染（diff 文本可显示）
    //   - DiffViewMode 属性可在子类访问（为 Phase 3.9b Split / SideBySide 切换铺路）
    public class DiffCodeEditor : CodeEditor
    {
        // 对照 WPF: [Null] private VisualPatch _visualPatch;
        private VisualPatch _visualPatch;

        // 对照 WPF: public DiffViewMode DiffViewMode { get; }
        public DiffViewMode DiffViewMode { get; }

        // 对照 WPF: [Null] public VisualPatch VisualPatch { get => _visualPatch; set => ... }
        // spike 版 setter 简化为：仅设置 base.Text，不做 highlighting / colorizer / scrollbar map
        public VisualPatch VisualPatch
        {
            get => _visualPatch;
            set
            {
                if (_visualPatch == value)
                {
                    return;
                }
                _visualPatch = value;

                // 对照 WPF: base.Text = _visualPatch?.StringValue ?? string.Empty
                // 把 VisualPatch 的 diff 字符串渲染到 CodeEditor（spike 不做语法高亮 / 着色）
                Text = _visualPatch?.StringValue ?? string.Empty;

                // Phase 3.9b 在此补：
                //   - _diffLineNumberMargin.UpdateLineNumbersData(_visualPatch)
                //   - _textColorizer.HunkHeaderLines = ...
                //   - _backgroundColorizer.HighlightingSource = CreateBackgroundHighlightingSource(...)
                //   - RefreshSyntaxHighlighting()
                //   - RefreshScrollbarMap()
                //   - InvalidateVisual()
            }
        }

        // 对照 WPF: public DiffCodeEditor() : this(DiffViewMode.Split)
        public DiffCodeEditor() : this(DiffViewMode.Split)
        {
        }

        // 对照 WPF: public DiffCodeEditor(DiffViewMode diffViewMode)
        public DiffCodeEditor(DiffViewMode diffViewMode)
        {
            DiffViewMode = diffViewMode;

            // Phase 3.9b 在此补：
            //   - _backgroundColorizer = new DiffBackgroundColorizer();
            //   - TextArea.TextView.BackgroundRenderers.Add(_backgroundColorizer);
            //   - _textColorizer = new DiffTextColorizer();
            //   - TextArea.TextView.LineTransformers.Add(_textColorizer);
            //   - _syntaxHighlighting = new SyntaxHighlighting();
            //   - TextArea.TextView.LineTransformers.Add(_syntaxHighlighting);
            //   - _diffLineNumberMargin = new DiffLineNumberMargin(diffViewMode);
            //   - TextArea.LeftMargins.Add(_diffLineNumberMargin);
            //   - 订阅 NotificationCenter.ApplicationThemeChanged / DisableSyntaxHighlightingChanged
        }

        // 对照 WPF: protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        // spike 版暂不实现（RefreshScrollbarMap 留 Phase 3.9b）

        // 对照 WPF: private void RefreshScrollbarMap() — Phase 3.9b
        // 对照 WPF: private int GetChangeLineNumber(VisualLine[] visualLines, VisualSubChunk visualSubChunk) — Phase 3.9b
        // 对照 WPF: private void AddLine(StreamGeometryContext ctx, ...) — Phase 3.9b
        // 对照 WPF: private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e) — Phase 3.9b
        // 对照 WPF: private void DisableSyntaxHighlightingChanged(object sender, EventArgs<bool> e) — Phase 3.9b
        // 对照 WPF: private void RefreshSyntaxHighlighting() — Phase 3.9b
        // 对照 WPF: private static HighlightingSource[] CreateBackgroundHighlightingSource(HighlightingScheme scheme) — Phase 3.9b
        // 对照 WPF: private static List<HighlightingSource> GetBackgroundHighlightingSource(Range[] regions, HighlightingType highlightingType) — Phase 3.9b
    }
}
