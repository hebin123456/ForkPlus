using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.9a：Avalonia 版 FileDiffControl 容器骨架（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/FileDiffControl.cs（973 行，纯 C# 无 XAML）：
    //   - 继承 DiffControlContainer : Grid（代码生成 Grid 2 行）：
    //     Row 0 (Auto): FileControlHeaderUserControl（默认 Collapsed）
    //     Row 1 (*):    动态切换的 _subView（ShowSubView<TChild>）
    //   - 5 种 sub-view（按 DiffContent 类型路由）：
    //     TextDiffControl     — 文本 diff（**AvalonEdit**，Phase 2.6 难点）
    //     BinaryDiffUserControl — 图片/二进制 diff
    //     HexDiffUserControl    — 16 进制 diff（**AvalonEdit** 衍生 HexEditor，Phase 2.9）
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
    // AvalonEdit 嵌入路径（spike 不迁移）：
    //   FileDiffControl → TextDiffControl → DiffCodeEditor : CodeEditor
    //     （CodeEditor 是 ICSharpCode.AvalonEdit.TextEditor 子类）
    //   全工程 30 个文件引用 ICSharpCode.AvalonEdit
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
    // 本 spike 版策略：**不引入 AvaloniaEdit**（留给 Phase 2.6 独立 spike 验证）
    //   - FileDiffControl 容器骨架（Grid 2 行 + Header + ContentControl 占位）
    //   - 5 种 sub-view 用 Border + TextBlock 占位，由 .cs 的 ShowSubView 根据字符串切换
    //   - 4 个公共属性替代 WPF DependencyProperty（spike 用 plain property，待 Phase 2.3 改 StyledProperty）
    //   - UpdateView stub：根据 Content 类型切换 5 个占位
    //
    // 本 spike 版暂不迁移：
    //   - AvalonEdit 子树（TextDiffControl / DiffCodeEditor / SideBySide / Split /
    //     DiffBackgroundColorizer / DiffTextColorizer / DiffLineNumberMargin / DiffSelectionLayer）
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
    //   - SubView 占位文字随 Content 类型切换
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

        // ===== ShowSubView 占位（对照 WPF ShowSubView<TChild> 动态装载）=====
        // spike 版根据 subViewType 字符串切换 TextBlock 文字，不真实装载 UserControl
        protected void ShowSubView(string subViewType)
        {
            Console.WriteLine($"[FileDiffControl] ShowSubView (spike placeholder): {subViewType}");
            if (SubViewPlaceholder != null)
            {
                SubViewPlaceholder.Text = subViewType switch
                {
                    "Text" => "(text diff placeholder — AvalonEdit not migrated, see Phase 2.6)",
                    "Binary" => "(binary diff placeholder — image swipe/onion skin not migrated)",
                    "Hex" => "(hex diff placeholder — HexEditor not migrated, see Phase 2.9)",
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
    }
}
