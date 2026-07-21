using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 HeaderMenuItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/HeaderMenuItem.cs（14 行）：
    //   - WPF HeaderMenuItem : MenuItem
    //   - 构造函数接收 string title
    //   - base.Header = PreferencesLocalization.MenuHeader(title)
    //   - base.IsEnabled = false（菜单标题项不可点击）
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 MenuItem）：
    //   1. 基类 MenuItem → Avalonia.Controls.MenuItem（API 一致）
    //   2. WPF base.Header = PreferencesLocalization.MenuHeader(title)
    //      → spike 直接 Header = title（spike 不依赖 PreferencesLocalization）
    //      task spec 简化策略：保留 Header 属性，spike 不做菜单头大小写转换
    //   3. WPF base.IsEnabled = false → Avalonia IsEnabled = false（API 一致）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 MenuItem
    //   - 构造函数接收 string title → Header = title
    //   - IsEnabled = false（菜单标题项不可点击）
    public class HeaderMenuItem : MenuItem
    {
        // 对照 WPF: public HeaderMenuItem(string title)
        //   base.Header = PreferencesLocalization.MenuHeader(title);
        //   base.IsEnabled = false;
        public HeaderMenuItem(string title)
        {
            Header = title;
            IsEnabled = false;
        }
    }
}
