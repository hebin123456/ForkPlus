using ForkPlus.Git;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 StashSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/StashSidebarItem.cs（17 行）：
    //   - WPF StashSidebarItem : SidebarItem
    //   - StashRevision Stash { get; }
    //   - string Tooltip { get; }
    //   - 构造函数 StashSidebarItem(string title, SidebarItem parent, StashRevision stash)
    //   - 依赖：ForkPlus.Git.StashRevision（Core 可用，继承 Revision 有 Message 属性）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 无 WPF 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 SidebarItem
    //   - Stash + Tooltip
    public class StashSidebarItem : SidebarItem
    {
        // 对照 WPF: public StashRevision Stash { get; }
        public StashRevision Stash { get; }

        // 对照 WPF: public string Tooltip { get; }
        public string Tooltip { get; }

        // 对照 WPF: public StashSidebarItem(string title, SidebarItem parent, StashRevision stash)
        public StashSidebarItem(string title, SidebarItem parent, StashRevision stash)
            : base(title, parent)
        {
            Stash = stash;
            Tooltip = stash.Message;
        }
    }
}
