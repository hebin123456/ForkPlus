using System;
using ForkPlus.Git;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 MainWorktreeSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/MainWorktreeSidebarItem.cs（18 行）：
    //   - WPF MainWorktreeSidebarItem : FolderSidebarItem
    //   - Worktree Worktree { get; }
    //   - string Tooltip { get; }（= worktree.GetTooltip()，WorktreeExtensions 扩展方法）
    //   - 构造函数 MainWorktreeSidebarItem(string title, SidebarItem parent, Worktree worktree, SidebarUserControl sidebarUserControl)
    //   - 依赖：ForkPlus.Git.Worktree（Core 可用，struct）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF worktree.GetTooltip()（WorktreeExtensions，ForkPlus.UI 命名空间）→ spike 内联逻辑
    //      （WorktreeExtensions 在 WPF 工程，spike 未迁移，直接内联 GetTooltip 实现）
    //   2. WPF SidebarUserControl（强类型）→ object（spike 替代具体依赖，透传基类）
    //   3. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 FolderSidebarItem
    //   - Worktree + Tooltip（内联 WorktreeExtensions.GetTooltip 逻辑）
    public class MainWorktreeSidebarItem : FolderSidebarItem
    {
        // 对照 WPF: public Worktree Worktree { get; }
        public Worktree Worktree { get; }

        // 对照 WPF: public string Tooltip { get; }
        public string Tooltip { get; }

        // 对照 WPF: public MainWorktreeSidebarItem(string title, SidebarItem parent, Worktree worktree, SidebarUserControl sidebarUserControl)
        //   Tooltip = worktree.GetTooltip();（WorktreeExtensions 扩展方法）
        // spike: WorktreeExtensions 在 WPF 工程（ForkPlus.UI），spike 内联 GetTooltip 逻辑
        public MainWorktreeSidebarItem(string title, SidebarItem parent, Worktree worktree, object sidebarUserControl)
            : base(title, parent, sidebarUserControl)
        {
            Worktree = worktree;
            Tooltip = GetWorktreeTooltip(worktree);
        }

        // spike 内联：对照 WPF WorktreeExtensions.GetTooltip(this Worktree)
        //   （src/ForkPlus/UI/WorktreeExtensions.cs，ForkPlus.UI 命名空间，spike 未迁移）
        private static string GetWorktreeTooltip(Worktree worktree)
        {
            if (worktree.HeadString.StartsWith("refs/heads/"))
            {
                string text = worktree.HeadString.Substring("refs/heads/".Length);
                return "Worktree:\t" + worktree.FriendlyName + Environment.NewLine
                    + "Location:\t\t" + worktree.Path + Environment.NewLine
                    + "Branch:\t\t" + text;
            }
            return "Worktree:\t" + worktree.FriendlyName + Environment.NewLine
                + "Location:\t\t" + worktree.Path + Environment.NewLine
                + "HEAD:\t\t" + worktree.HeadString.Abbreviated();
        }
    }
}
