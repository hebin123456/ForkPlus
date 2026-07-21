using System;
using Avalonia.Controls;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI;
// 当前命名空间 ForkPlus.Avalonia.Controls.Editor.Diff 的末段 "Diff" 与
// ForkPlus.Git.Diff.Diff 类型名冲突（编译器优先把 Diff 解析为当前命名空间），
// 用类型别名显式指向 ForkPlus.Git.Diff.Diff。
using Diff = ForkPlus.Git.Diff.Diff;

namespace ForkPlus.Avalonia.Controls.Editor.Diff
{
    // Phase 2.6：Avalonia 版 TextDiffControl spike（CodeEditor 容器）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Diff/TextDiffControl.cs（185 行）：
    //   - WPF TextDiffControl : Grid, IFileDiffControlSubControl
    //   - 字段：_layoutMode / _child (ITextDiffControl) / _target (FileDiffControlTarget)
    //   - 构造函数：订阅 NotificationCenter 的 4 个事件 +
    //     RefreshDiffLayoutMode / RefreshDiffWordWrap / RefreshDiffShowHiddenSymbols / RefreshDiffFontSize
    //   - LayoutMode setter：调 RefreshLayout（销毁 _child + 根据 mode 创建 Split/SideBySide）
    //   - SetDiff(diff, tabWidth, entireFile, location)：转发给 _child
    //   - RefreshLayout()：根据 LayoutMode 创建 SplitTextDiffControl / SideBySideTextDiffControl
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. UserControl 替代 Grid 子类（Avalonia 习惯用 axaml UserControl 包装）
    //   2. spike 版只用单个 DiffCodeEditor（不切换 Split / SideBySide，留 Phase 3.9b）
    //   3. SetDiff 简化为：从 diff 字符串构造 VisualPatch 并赋给 DiffCodeEditor.VisualPatch
    //   4. 跳过 NotificationCenter 4 个事件订阅（DiffLayoutMode / DiffShowHiddenSymbols /
    //      DiffWordWrap / CodeEditorFontSize，留 Phase 3.9b）
    //   5. 跳过 PositionCache / VerticalScrollBarVisibility 公共属性（Phase 3.9b）
    //
    // 本 spike 版暂不迁移（留 Phase 3.9b）：
    //   - SplitTextDiffControl / SideBySideTextDiffControl（双布局切换）
    //   - ITextDiffControl 接口（spike 直接用具体类）
    //   - DiffLayoutMode / DiffShowHiddenSymbols / DiffWordWrap / CodeEditorFontSize
    //     四个 NotificationCenter 事件订阅
    //   - PositionCache（CodeEditorScrollPositionCache，滚动位置缓存）
    //   - EditorContextMenuOpening 事件
    //   - ScrollToNextCustomHunk / ScrollToPreviousCustomHunk（chunk 间跳转）
    //   - FileDiffControlTarget 真实使用（spike 用字符串占位）
    //
    // 本 spike 版验证：
    //   - DiffCodeEditor 可在 axaml 中实例化（AvaloniaEdit.TextEditor 子类树可工作）
    //   - VisualPatch 可作为 TextDiffControl 的 diff 数据入口
    //   - 整个 AvalonEdit → AvaloniaEdit 子树在 Avalonia 11.3.18 下可编译可运行
    public partial class TextDiffControl : UserControl
    {
        // 对照 WPF: private DiffLayoutMode _layoutMode
        // spike 版仅存储值，不做 Split / SideBySide 切换（Phase 3.9b）
        private DiffLayoutMode _layoutMode;

        // 对照 WPF: private readonly FileDiffControlTarget _target
        // spike 版用字符串占位（FileDiffControlTarget 在 WPF 工程，Avalonia 工程不可访问）
        private readonly string _target;

        // 对照 WPF: public DiffLayoutMode LayoutMode { get => _layoutMode; set => { _layoutMode = value; RefreshLayout(); } }
        // spike 版 setter 仅存储值，不调 RefreshLayout（Phase 3.9b）
        public DiffLayoutMode LayoutMode
        {
            get => _layoutMode;
            set
            {
                _layoutMode = value;
                Console.WriteLine($"[TextDiffControl] LayoutMode set to {value} (spike: no actual layout switch, Phase 3.9b)");
                // Phase 3.9b 在此调 RefreshLayout() 切换 Split/SideBySide
            }
        }

        public TextDiffControl()
        {
            InitializeComponent();
            _target = "Revision"; // 对照 WPF 默认 FileDiffControlTarget.Revision
            _layoutMode = DiffLayoutMode.Split; // 对照 WPF 默认 DiffLayoutMode.Split
        }

        // 对照 WPF: public TextDiffControl(FileDiffControlTarget target)
        // spike 版省略带参构造（FileDiffControlTarget 在 WPF 工程，Avalonia 工程不可访问；
        //   spike 阶段从 axaml 实例化只能用默认构造；Phase 3.9b 重构时再补 Target 属性）

        // 对照 WPF: public void SetDiff([Null] Diff diff, int tabWidth, bool entireFile, DiffLocation location)
        // spike 版：从 diff 构造 VisualPatch 赋给 DiffCodeEditor.VisualPatch
        // 注意：VisualPatch.CreateVisualPatch 在 Core 工程，需要真实 Diff 对象；
        //       spike 阶段 diff 通常为 null，会触发 VisualPatch.CreateVisualPatch(null, ...) 返回 null，
        //       DiffCodeEditor.VisualPatch setter 会清空 Text，editor 显示空白。
        public void SetDiff(Diff diff, int tabWidth, bool entireFile, DiffLocation location)
        {
            Console.WriteLine($"[TextDiffControl] SetDiff (spike): diff={diff?.ToString() ?? "null"}, tabWidth={tabWidth}, entireFile={entireFile}, location={location}");

            if (DiffEditor == null)
            {
                Console.WriteLine("[TextDiffControl] DiffEditor not yet initialized");
                return;
            }

            // 构造 VisualPatch（Core 工程，跨平台可用）
            // Phase 3.9b 在此补：根据 LayoutMode 切换 Split / SideBySide 路径
            VisualPatch visualPatch = VisualPatch.CreateVisualPatch(diff, entireFile, location);
            DiffEditor.VisualPatch = visualPatch;
        }

        // 对照 WPF: public void ScrollToNextCustomHunk() — Phase 3.9b
        public void ScrollToNextCustomHunk()
        {
            Console.WriteLine("[TextDiffControl] ScrollToNextCustomHunk (spike: stub, Phase 3.9b)");
        }

        // 对照 WPF: public void ScrollToPreviousCustomHunk() — Phase 3.9b
        public void ScrollToPreviousCustomHunk()
        {
            Console.WriteLine("[TextDiffControl] ScrollToPreviousCustomHunk (spike: stub, Phase 3.9b)");
        }

        // 对照 WPF: public void ControlWillBeRemovedFromFileDiffControl()
        // spike 版无 _child 切换，无资源需要释放
        public void ControlWillBeRemovedFromFileDiffControl()
        {
            Console.WriteLine("[TextDiffControl] ControlWillBeRemovedFromFileDiffControl (spike: no-op)");
        }

        // Phase 3.9b 在此补：
        //   - RefreshLayout()：根据 LayoutMode 创建 SplitTextDiffControl / SideBySideTextDiffControl
        //   - RefreshDiffLayoutMode / RefreshDiffShowHiddenSymbols / RefreshDiffWordWrap / RefreshDiffFontSize
        //     四个 NotificationCenter 事件处理
        //   - PositionCache 公共属性
        //   - VerticalScrollBarVisibility 公共属性
        //   - EditorContextMenuOpening 公共事件
    }
}
