using System;
using ForkPlus.Git;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 TagSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/TagSidebarItem.cs（16 行）：
    //   - WPF TagSidebarItem : ReferenceSidebarItem
    //   - private Tag Tag => base.Reference as Tag
    //   - override string Tooltip => $"Tag '{Tag.Name}'{Environment.NewLine}{Tag.CommitterDate}"
    //   - 构造函数 TagSidebarItem(string title, SidebarItem parent, Tag tag)
    //   - 依赖：ForkPlus.Git.Tag（Core 可用，继承 Reference 有 Name/CommitterDate）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 无 WPF 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ReferenceSidebarItem
    //   - override Tooltip
    public class TagSidebarItem : ReferenceSidebarItem
    {
        // 对照 WPF: private Tag Tag => base.Reference as Tag;
        private Tag Tag => Reference as Tag;

        // 对照 WPF: public override string Tooltip => $"Tag '{Tag.Name}'{Environment.NewLine}{Tag.CommitterDate}";
        public override string Tooltip => $"Tag '{Tag.Name}'{Environment.NewLine}{Tag.CommitterDate}";

        // 对照 WPF: public TagSidebarItem(string title, SidebarItem parent, Tag tag)
        public TagSidebarItem(string title, SidebarItem parent, Tag tag)
            : base(title, parent, tag)
        {
        }
    }
}
