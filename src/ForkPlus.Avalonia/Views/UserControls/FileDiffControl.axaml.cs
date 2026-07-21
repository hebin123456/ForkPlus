using System;
using Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.9a + Phase 2.6 升级：Avalonia 版 FileDiffControl 容器骨架。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/FileDiffControl.cs（973 行，纯 C# 无 XAML）：
    //   - 继承 DiffControlContainer : Grid（代码生成 Grid 2 行）：
    //     Row 0 (Auto): FileControlHeaderUserControl（默认 Collapsed）
    //     Row 1 (*):    动态切换的 _subView（ShowSubView<TChild>）
    //   - 5 种 sub-view（按 DiffContent 类型路由）：
    //     TextDiffControl     — 文本 diff（**AvaloniaEdit**，Phase 2.6 已迁移基础版）
    //     BinaryDiffUserControl — 图片/二进制 diff
    //     HexDiffUserControl    — 16 进制 diff（**AvaloniaEdit** 衍生 HexEditor，Phase 2.9）
    //     SubmoduleDiffUserControl — 子模块 diff
    //     FallbackUserControl   — 错误/空状态
    //   - 4 个 DependencyProperty：
    //     Content (GitCommandResult<DiffContent>) — 核心数据入口
    //     RepositoryUserControl                    — 注入父控件
    //     Target (FileDiffControlTarget)           — Revision/History/Popup
    //     SubControlMode (bool)                    — 嵌入式 diff（RevisionSummary 用）
    //   - UpdateView(bool loadLargeDiff) 是核心调度器
    //   - 公共事件：ShowLargeUntrackedChanges
    //
    // AvaloniaEdit 嵌入路径（Phase 2.6 已打通）：
    //   FileDiffControl → TextDiffControl → DiffCodeEditor : CodeEditor
    //     （CodeEditor 是 AvaloniaEdit.TextEditor 子类，src/ForkPlus.Avalonia/Controls/Editor/）
    //   DiffCodeEditor spike 版用 AvaloniaEdit API：Options / TextArea.TextView.VerticalOffset
    //     / ScrollToVerticalOffset / SearchPanel
    //   Phase 3.9b 在此补：BackgroundRenderers / LineTransformers / LeftMargins
    //
    // 装入路径（WPF，FileDiffControl 共 7+ 处被装入）：
    //   CommitUserControl.xaml → CommitFileDiffControl（commit 视图）
    //   RevisionChangesUserControl.xaml → FileDiffControl（revision 详情的 Changes tab）
    //   RevisionSummaryUserControl.xaml → FileDiffControl SubControlMode=True（嵌入 summary 行内）
    //   MergeConflictUserControl.xaml → FileDiffControl
    //   FileHistoryWindow.xaml → FileDiffControl Target=History（Phase 4）
    //   AiCodeReviewWindow.xaml → CommitFileDiffControl（Phase 5）
    //   AiSuggestionPreviewWindow.xaml → FileDiffControl Target=Popup（Phase 5）
    //   DiffPopupWindow.xaml.cs → 代码 new FileDiffControl() / new CommitFileDiffControl()
    //
    // Phase 2.6 升级策略：**Text sub-view 改用真实 TextDiffControl**（解 Phase 3.9a 占位）
    //   - Text 类型：editor:TextDiffControl（DiffCodeEditor 容器，承载真实 diff 文本渲染）
    //   - Binary/Hex/Submodule/Fallback：仍用 TextBlock 占位（Phase 2.9/3.9b 迁移）
    //   - ShowSubView 根据 subViewType 切换 TextDiffControl.IsVisible / TextBlock.IsVisible
    //   - 4 个公共属性替代 WPF DependencyProperty（spike 用 plain property，待 Phase 2.3 改 StyledProperty）
    //   - UpdateView stub：根据 Content 类型路由到 5 个占位/真实控件
    //
    // 本 spike 版暂不迁移（留 Phase 3.9b）：
    //   - DiffCodeEditor 的 BackgroundRenderers / LineTransformers / LeftMargins（着色器 / 行号 / 高亮）
    //   - SplitTextDiffControl / SideBySideTextDiffControl（双布局切换）
    //   - DiffBackgroundColorizer / DiffTextColorizer / DiffLineNumberMargin / DiffSelectionLayer
    //   - HexEditor / HexDiffUserControl（Phase 2.9）
    //   - BinaryDiffUserControl 真实实现（图片 swipe / onion skin）
    //   - SubmoduleDiffUserControl 真实实现
    //   - PatchParser / LoadBinaryDiffContent / LoadSubmoduleDiffContent git 调用
    //   - HunkHistoryCommand / CopyAsPatchCommand 编辑器上下文菜单
    //   - CommitFileDiffControl 的 ToggleStage / Stage / UnStage / Discard chunk 级 patch apply
    //
    // 本 spike 版验证：
    //   - Grid 2 行布局正确显示
    //   - FileControlHeader 占位可见（默认 Collapsed，由 Show() 触发显示）
    //   - Text 类型时 TextDiffControl 可见（解 Phase 3.9a 占位）
    //   - 其他类型时 TextBlock 占位文字随 Content 类型切换
    public partial class FileDiffControl : UserControl
    {
        // ===== 公共事件（对照 WPF）=====
        public event EventHandler<EventArgs> ShowLargeUntrackedChanges;

        // ===== 公共属性（对照 WPF 4 个 DependencyProperty，spike 用 plain property）=====
        // spike 版用 object/string 占位，真实类型待 Phase 2.3（StyledProperty）+ Phase 3.9b 补
        public object Content { get; set; }
        public object RepositoryUserControl { get; set; }
        public string Target { get; set; } = "Revision"; // 对照 WPF FileDiffControlTarget.Revision
        public bool SubControlMode { get; set; }

        public FileDiffControl()
        {
            InitializeComponent();
        }

        // ===== ShowSubView（对照 WPF ShowSubView<TChild> 动态装载）=====
        // Phase 2.6 升级：Text 类型不再用 TextBlock 占位，改用真实 TextDiffControl。
        // Phase 2.9 升级：Hex 类型不再用 TextBlock 占位，改用真实 HexDiffUserControl。
        // 其他类型（Binary/Submodule/Fallback）仍用 TextBlock 占位（Phase 3.9b）。
        protected void ShowSubView(string subViewType)
        {
            Console.WriteLine($"[FileDiffControl] ShowSubView: {subViewType}");

            if (subViewType == "Text")
            {
                // Phase 2.6：显示 TextDiffControl（承载 DiffCodeEditor : CodeEditor : AvaloniaEdit.TextEditor）
                if (SubViewPlaceholder != null) SubViewPlaceholder.IsVisible = false;
                if (HexDiffSubView != null) HexDiffSubView.IsVisible = false;
                if (TextDiffSubView != null) TextDiffSubView.IsVisible = true;
                return;
            }

            if (subViewType == "Hex")
            {
                // Phase 2.9：显示 HexDiffUserControl（承载双 HexEditor : CodeEditor : AvaloniaEdit.TextEditor）
                if (SubViewPlaceholder != null) SubViewPlaceholder.IsVisible = false;
                if (TextDiffSubView != null) TextDiffSubView.IsVisible = false;
                if (HexDiffSubView != null) HexDiffSubView.IsVisible = true;
                return;
            }

            // 非 Text/Hex 类型：隐藏 TextDiffControl + HexDiffUserControl，显示 TextBlock 占位
            if (TextDiffSubView != null) TextDiffSubView.IsVisible = false;
            if (HexDiffSubView != null) HexDiffSubView.IsVisible = false;
            if (SubViewPlaceholder != null)
            {
                SubViewPlaceholder.IsVisible = true;
                SubViewPlaceholder.Text = subViewType switch
                {
                    "Binary" => "(binary diff placeholder — image swipe/onion skin not migrated)",
                    "Submodule" => "(submodule diff placeholder)",
                    "Fallback" => "(no diff / fallback)",
                    _ => $"(unknown sub-view: {subViewType})"
                };
            }
        }

        // ===== UpdateView 占位（对照 WPF UpdateView 核心调度器）=====
        // spike 版根据 Content 类型路由到 5 个占位 sub-view，不调 git 命令
        public virtual void UpdateView(bool loadLargeDiff = false)
        {
            Console.WriteLine($"[FileDiffControl] UpdateView (spike placeholder): loadLargeDiff={loadLargeDiff}, Content={Content}");

            // spike 版用 Content 的 ToString 简单判断类型，真实逻辑待 Phase 3.9b
            string subViewType = Content?.ToString() switch
            {
                "Text" => "Text",
                "Binary" => "Binary",
                "Hex" => "Hex",
                "Submodule" => "Submodule",
                null => "Fallback",
                _ => "Fallback"
            };
            ShowSubView(subViewType);
        }

        // 对照 WPF: public void Show(FileControlHeaderMode mode)
        //   显示 FileControlHeader 并设置 mode
        public void Show(string mode = "None")
        {
            Console.WriteLine($"[FileDiffControl] Show (spike placeholder): mode={mode}");
            if (FileControlHeader != null)
            {
                FileControlHeader.IsVisible = true;
            }
        }

        // Phase 2.9：HexDiffUserControl 的数据入口（对照 WPF 通过 _subView.SetContent(content) 转发）。
        // spike 版需要外部调用者把 HexDiffContent 显式传入（WPF 是通过 UpdateView 内部 dispatch 自动转发，
        // spike 版 UpdateView 暂不接入 git 命令，调用方手动调 SetHexDiff 加载字节）。
        public void SetHexDiff(HexDiffContent content)
        {
            Console.WriteLine($"[FileDiffControl] SetHexDiff (Phase 2.9): srcSize={content?.SrcSize}, dstSize={content?.DstSize}");
            HexDiffSubView?.SetContent(content);
        }

        // Phase 2.6：TextDiffControl 的数据入口（对照 WPF 通过 _subView.SetDiff(diff, ...) 转发）。
        // spike 版需要外部调用者把 Diff 显式传入。
        public void SetTextDiff(Diff diff, int tabWidth, bool entireFile, DiffLocation location)
        {
            Console.WriteLine($"[FileDiffControl] SetTextDiff (Phase 2.6): diff={diff?.ToString() ?? "null"}");
            TextDiffSubView?.SetDiff(diff, tabWidth, entireFile, location);
        }
    }
}
