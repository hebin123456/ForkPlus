using ForkPlus.Services;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 SidebarGroupItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/SidebarGroupItem.cs（44 行）：
    //   - WPF SidebarGroupItem : FolderSidebarItem
    //   - enum Group { Pinned, Branches, Remotes, Tags, Stashes, Submodules, Worktrees }
    //   - Group GroupType { get; }
    //   - void RefreshTitle()（PreferencesLocalization.Current(GroupType.ToString())）
    //   - override GetDropEffect/Drop（空实现，禁止拖放）
    //   - 构造函数接收 SidebarUserControl
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF PreferencesLocalization.Current → ServiceLocator.Localization.Current
    //   2. WPF SidebarUserControl（强类型）→ object（透传基类，spike 替代）
    //   3. WPF DragEventArgs/DragDropEffects → 省略拖拽逻辑（保留空方法 + 注释）
    //   4. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 FolderSidebarItem
    //   - Group 枚举（Pinned/Branches/Remotes/Tags/Stashes/Submodules/Worktrees）
    //   - GroupType + RefreshTitle（ServiceLocator.Localization）
    public class SidebarGroupItem : FolderSidebarItem
    {
        // 对照 WPF: public enum Group { Pinned, Branches, Remotes, Tags, Stashes, Submodules, Worktrees }
        public enum Group
        {
            Pinned,
            Branches,
            Remotes,
            Tags,
            Stashes,
            Submodules,
            Worktrees
        }

        // 对照 WPF: public Group GroupType { get; }
        public Group GroupType { get; }

        // 对照 WPF: public SidebarGroupItem(string title, SidebarItem parent, Group group, SidebarUserControl sidebarUserControl)
        public SidebarGroupItem(string title, SidebarItem parent, Group group, object sidebarUserControl)
            : base(title, parent, sidebarUserControl)
        {
            GroupType = group;
        }

        // 对照 WPF: public void RefreshTitle()
        //   Title = UserControls.Preferences.PreferencesLocalization.Current(GroupType.ToString());
        //   RaisePropertyChanged(nameof(Title));
        // spike: PreferencesLocalization.Current → ServiceLocator.Localization.Current
        public void RefreshTitle()
        {
            Title = ServiceLocator.Localization.Current(GroupType.ToString());
            RaisePropertyChanged(nameof(Title));
        }

        // 对照 WPF: public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
        //   e.Handled = true; return DragDropEffects.None;
        public override int GetDropEffect(object e, int index)
        {
            // spike: WPF 拖拽逻辑省略（返回 0 = DragDropEffects.None）
            return 0;
        }

        // 对照 WPF: public override void Drop(DragEventArgs e, int index)
        //   e.Effects = DragDropEffects.None; e.Handled = true;
        public override void Drop(object e, int index)
        {
            // spike: WPF Drop 逻辑省略
        }
    }
}
