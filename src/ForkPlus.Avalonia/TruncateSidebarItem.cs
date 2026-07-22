namespace ForkPlus.Avalonia
{
    // Avalonia 版 TruncateSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/TruncateSidebarItem.cs（11 行）：
    //   - WPF TruncateSidebarItem : SidebarItem
    //   - override bool IsFocusable => false
    //   - 构造函数 TruncateSidebarItem(string title, SidebarItem parent)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 无 WPF 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 SidebarItem
    //   - override IsFocusable => false
    public class TruncateSidebarItem : SidebarItem
    {
        // 对照 WPF: public override bool IsFocusable => false;
        public override bool IsFocusable => false;

        // 对照 WPF: public TruncateSidebarItem(string title, SidebarItem parent)
        public TruncateSidebarItem(string title, SidebarItem parent)
            : base(title, parent)
        {
        }
    }
}
