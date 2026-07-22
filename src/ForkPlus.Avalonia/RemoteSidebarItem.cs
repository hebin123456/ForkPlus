using ForkPlus.Avalonia.Controls;
using ForkPlus.Git;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 RemoteSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/RemoteSidebarItem.cs（39 行）：
    //   - WPF RemoteSidebarItem : FolderSidebarItem
    //   - Remote Remote { get; }
    //   - string Tooltip { get; }（= remote.Url）
    //   - ImageSource RemoteIcon => Remote.GetIconImage()（WPF 专有扩展方法，返回 ImageSource）
    //   - override GetDropEffect/Drop（空实现，禁止拖放）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF ImageSource RemoteIcon → object（spike 替代图片类型）
    //   2. WPF Remote.GetIconImage() 扩展方法（返回 ImageSource）→ spike 返回 null
    //      （GetIconImage 为 WPF 专有 BridgeExtensions，spike 未迁移图标解析逻辑）
    //   3. WPF DragEventArgs/DragDropEffects → 省略拖拽逻辑（保留空方法 + 注释）
    //   4. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 FolderSidebarItem
    //   - Remote + RemoteIcon（object 替代 ImageSource，spike 返回 null）
    //   - GetDropEffect/Drop override 空实现
    public class RemoteSidebarItem : FolderSidebarItem
    {
        // 对照 WPF: public Remote Remote { get; }
        public Remote Remote { get; }

        // 对照 WPF: public string Tooltip { get; }
        public string Tooltip { get; }

        // 对照 WPF: public ImageSource RemoteIcon => Remote.GetIconImage();
        // spike: WPF ImageSource → object；GetIconImage 为 WPF 专有扩展，spike 返回 null
        public object RemoteIcon => null;

        // 对照 WPF: public RemoteSidebarItem(string title, SidebarItem parent, Remote remote, SidebarUserControl sidebarUserControl)
        public RemoteSidebarItem(string title, SidebarItem parent, Remote remote, object sidebarUserControl)
            : base(title, parent, sidebarUserControl)
        {
            Remote = remote;
            Tooltip = remote.Url;
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
