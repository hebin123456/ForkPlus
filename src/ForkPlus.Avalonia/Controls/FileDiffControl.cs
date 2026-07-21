using System;
using Avalonia.Controls;
using AvaloniaEdit;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 FileDiffControl（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/FileDiffControl.cs（973 行）：
    //   - WPF FileDiffControl : DiffControlContainer : Grid
    //   - Grid 2 行：Row 0 (Auto) FileControlHeaderUserControl（默认 Collapse）
    //                  Row 1 (*)    动态切换的 _subView（ShowSubView<TChild>）
    //   - 4 个 DependencyProperty：
    //     Content (GitCommandResult<DiffContent>) — 核心数据入口
    //     RepositoryUserControl                    — 注入父控件
    //     Target (FileDiffControlTarget)           — Revision/History/Popup
    //     SubControlMode (bool)                    — 嵌入式 diff（RevisionSummary 用）
    //   - UpdateView(bool loadLargeDiff) 是核心调度器
    //   - 公共事件：ShowLargeUntrackedChanges
    //   - 5 种 sub-view（按 DiffContent 类型路由）：TextDiffControl / BinaryDiffUserControl /
    //     HexDiffUserControl / SubmoduleDiffUserControl / FallbackUserControl
    //
    // 已升级版本（task spec: 检查并升级）：
    //   - Avalonia 端已有 src/ForkPlus.Avalonia/Views/UserControls/FileDiffControl.axaml + .cs
    //     （namespace ForkPlus.Avalonia.Views.UserControls），已升级：
    //     * 继承 UserControl（Grid 2 行布局）
    //     * Text sub-view 用真实 TextDiffControl（DiffCodeEditor : CodeEditor : AvaloniaEdit.TextEditor）
    //     * Hex sub-view 用真实 HexDiffUserControl（双 HexEditor）
    //     * 4 个 plain property 替代 WPF DependencyProperty（Content 字段名与 WPF 一致）
    //     * UpdateView 占位 + SetTextDiff / SetHexDiff 数据入口
    //   - 本 spike 文件（namespace ForkPlus.Avalonia.Controls）是更简单的独立 spike，
    //     用 AvaloniaEdit.TextEditor 直接显示 diff 文本，不依赖 TextDiffControl。
    //
    // Avalonia 版差异（spike 简化策略，task spec：用 AvaloniaEdit.TextEditor 显示 diff）：
    //   1. WPF DiffControlContainer : Grid 基类 → spike 直接继承 UserControl
    //   2. WPF DependencyProperty.Register → spike 用 plain property（与已升级版一致）
    //   3. WPF OnPreviewKeyDown → Avalonia KeyDown（无 Preview tunneling）
    //   4. WPF OnPropertyChanged(DependencyPropertyChangedEventArgs) → spike 用 plain property setter
    //   5. WPF 5 种 sub-view 路由 → spike 仅用 1 个 AvaloniaEdit.TextEditor 显示 diff 文本
    //   6. WPF TextDiffControl → spike 直接用 AvaloniaEdit.TextEditor（无 DiffCodeEditor 着色器）
    //   7. spike 跳过 PatchParser / LoadBinaryDiffContent / LoadSubmoduleDiffContent git 调用
    //   8. spike 跳过 HunkHistoryCommand / CopyAsPatchCommand 编辑器上下文菜单
    //   9. WPF Content (DependencyProperty) 与 Avalonia ContentControl.Content 重名，
    //      spike 用 DiffData 属性承载 diff 数据，避免遮蔽基类 Content（视觉承载）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 UserControl
    //   - 内嵌 1 个 AvaloniaEdit.TextEditor 显示 diff（通过 base.Content 承载视觉）
    //   - SetDiff(string diffText) 公共方法加载 diff 文本
    //   - 4 个公共属性（DiffData 替代 WPF Content，避免遮蔽）+ ShowLargeUntrackedChanges 事件
    public class FileDiffControl : UserControl
    {
        // 对照 WPF: public EventHandler ShowLargeUntrackedChanges
        public event EventHandler<EventArgs> ShowLargeUntrackedChanges;

        // 对照 WPF: public static readonly FileDiffControlCommands Commands
        // spike: 引用同命名空间的 FileDiffControlCommands 静态类
        // （已迁移到 Controls/FileDiffControlCommands.cs）

        // 对照 WPF: 4 个 DependencyProperty（Content / RepositoryUserControl / Target / SubControlMode）
        // spike: Content 重命名为 DiffData 避免遮蔽 UserControl.Content（视觉承载）
        //         其他 3 个属性名与 WPF 一致
        public object DiffData { get; set; }
        public object RepositoryUserControl { get; set; }
        public string Target { get; set; } = "Revision"; // 对照 WPF FileDiffControlTarget.Revision
        public bool SubControlMode { get; set; }

        // spike: 内嵌 AvaloniaEdit.TextEditor 显示 diff 文本
        // 对照 WPF: TextDiffControl → DiffCodeEditor : CodeEditor : ICSharpCode.AvalonEdit.TextEditor
        private readonly TextEditor _editor;

        public FileDiffControl()
        {
            // spike: 用 AvaloniaEdit.TextEditor 直接显示 diff（task spec 关键 API）
            // 通过 base.Content 承载视觉（不遮蔽 ContentControl.Content）
            _editor = new TextEditor
            {
                IsReadOnly = true,
                ShowLineNumbers = true,
                WordWrap = false
            };
            Content = _editor;
        }

        // spike 公共方法：加载 diff 文本到 AvaloniaEdit.TextEditor
        // 对照 WPF: TextDiffControl.SetDiff(diff, tabWidth, entireFile, location)
        // spike: 直接接收已格式化的 diff 文本字符串（跳过 Diff 对象解析）
        public void SetDiff(string diffText)
        {
            _editor.Text = diffText ?? string.Empty;
        }

        // 对照 WPF: protected virtual void UpdateView(bool loadLargeDiff = false)
        // spike: 占位，不调 git 命令（与已升级版一致）
        public virtual void UpdateView(bool loadLargeDiff = false)
        {
            // spike: 真实路由逻辑在已升级版 Views/UserControls/FileDiffControl.axaml.cs
            // 本 spike 仅占位
        }
    }
}
