using System;
using Avalonia.Controls;
using AvaloniaEdit;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 FileMergeControl（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/FileMergeControl.cs（200 行）：
    //   - WPF FileMergeControl : FileDiffControl（继承 FileDiffControl 的所有功能）
    //   - 重写 UpdateView(bool loadLargeDiff)：
    //     UnmergedDiffContent (Lfs/Binary/Submodule/Text) → BinaryDiffUserControl / SubmoduleDiffUserControl
    //     BinaryDiffContent → BinaryDiffUserControl
    //     UnknownBinaryDiffContent → BinaryDiffUserControl
    //     LfsDiffContent → BinaryDiffUserControl
    //     SubmoduleDiffContent → SubmoduleDiffUserControl
    //   - 与 FileDiffControl 区别：merge 视图隐藏 header（h.Hide() / h.Collapse()），
    //     showTitle=false，ViewMode.Merge
    //   - 复用 FileDiffControl.LoadUnmergedBinaryDiffContent / LoadUnmergedUnknownBinaryDiffContent /
    //     LoadUnmergedSubmoduleDiffContent 静态方法
    //
    // Avalonia 版差异（spike 简化策略，task spec：用 AvaloniaEdit.TextEditor 显示合并视图）：
    //   1. WPF FileMergeControl : FileDiffControl → spike 继承 FileDiffControl（同命名空间）
    //   2. WPF UpdateView 重写（5 种 UnmergedDiffContent 路由）→ spike 用 1 个 AvaloniaEdit.TextEditor
    //   3. spike 跳过 BinaryDiffUserControl / SubmoduleDiffUserControl 子视图
    //   4. spike 跳过 LoadUnmerged* git 命令调用
    //   5. spike 跳过 showTitle=false / ViewMode.Merge 标志
    //   6. spike 复用 FileDiffControl.SetDiff 加载合并文本
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 FileDiffControl（同命名空间 ForkPlus.Avalonia.Controls）
    //   - 重写 UpdateView 占位
    //   - SetMergeText(string) 公共方法加载合并视图文本
    public class FileMergeControl : FileDiffControl
    {
        public FileMergeControl()
        {
            // spike: 复用基类 FileDiffControl 的 AvaloniaEdit.TextEditor
            // 对照 WPF: FileMergeControl : FileDiffControl（继承所有功能）
        }

        // 对照 WPF: protected override void UpdateView(bool loadLargeDiff = false)
        // spike: 重写占位，不调 git 命令
        public override void UpdateView(bool loadLargeDiff = false)
        {
            // spike: 真实合并视图路由待 Phase 3.9b
            // WPF 版路由 UnmergedDiffContent → BinaryDiffUserControl / SubmoduleDiffUserControl
        }

        // spike 公共方法：加载合并视图文本
        // 对照 WPF: 各 sub-view 的 UpdateDiff(repositoryUserControl, content, showTitle: false)
        // spike: 直接调用基类 SetDiff 加载合并文本到 AvaloniaEdit.TextEditor
        public void SetMergeText(string mergeText)
        {
            SetDiff(mergeText);
        }
    }
}
