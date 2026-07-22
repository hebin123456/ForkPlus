using ForkPlus.Git;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 SubmoduleSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/SubmoduleSidebarItem.cs（36 行）：
    //   - WPF SubmoduleSidebarItem : SidebarItem
    //   - private bool _isDirty + IsDirty 属性（set 时更新 Title 加 "*" 后缀）
    //   - private string SubmoduleName { get; }
    //   - Submodule Submodule { get; }
    //   - 构造函数 SubmoduleSidebarItem(string title, SidebarItem parent, Submodule submodule)
    //   - 依赖：ForkPlus.Git.Submodule（Core 可用）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 无 WPF 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //   3. INotifyPropertyChanged 继承自 MultiselectionTreeViewItem（spike 已有）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 SidebarItem
    //   - Submodule + IsDirty（Title 动态更新）
    public class SubmoduleSidebarItem : SidebarItem
    {
        // 对照 WPF: private bool _isDirty;
        private bool _isDirty;

        // 对照 WPF: public bool IsDirty
        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    Title = _isDirty ? (SubmoduleName + "*") : SubmoduleName;
                    RaisePropertyChanged(nameof(Title));
                }
            }
        }

        // 对照 WPF: private string SubmoduleName { get; }
        private string SubmoduleName { get; }

        // 对照 WPF: public Submodule Submodule { get; }
        public Submodule Submodule { get; }

        // 对照 WPF: public SubmoduleSidebarItem(string title, SidebarItem parent, Submodule submodule)
        public SubmoduleSidebarItem(string title, SidebarItem parent, Submodule submodule)
            : base(title, parent)
        {
            Submodule = submodule;
            SubmoduleName = title;
        }
    }
}
