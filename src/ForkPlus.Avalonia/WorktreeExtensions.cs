using System;
using ForkPlus.Git;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/WorktreeExtensions.cs（17 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - GetTooltip(this Worktree worktree) → 格式化 Worktree / Location / Branch|HEAD 信息
    //     分支：HeadString.StartsWith("refs/heads/") → 显示 Branch 名
    //     非分支：显示 HeadString.Abbreviated()（7 字符 SHA 缩写）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. Worktree 类型来自 ForkPlus.Core（ForkPlus.Git.Worktree struct），Avalonia 工程引用 Core 可直接用
    //   2. Environment.NewLine 跨平台可用，无需改动
    //   3. HeadString.Abbreviated() 来自 ForkPlus.Core.StringExtensions（ForkPlus 命名空间），
    //      通过父命名空间查找自动可用（ForkPlus.Avalonia 是 ForkPlus 子命名空间）
    //   4. 本文件为直接迁移，无 API 变更（纯字符串格式化，无 UI 依赖）
    public static class WorktreeExtensions
    {
        public static string GetTooltip(this Worktree worktree)
        {
            if (worktree.HeadString.StartsWith("refs/heads/"))
            {
                string text = worktree.HeadString.Substring("refs/heads/".Length);
                return "Worktree:\t" + worktree.FriendlyName + Environment.NewLine + "Location:\t\t" + worktree.Path + Environment.NewLine + "Branch:\t\t" + text;
            }
            return "Worktree:\t" + worktree.FriendlyName + Environment.NewLine + "Location:\t\t" + worktree.Path + Environment.NewLine + "HEAD:\t\t" + worktree.HeadString.Abbreviated();
        }
    }
}
