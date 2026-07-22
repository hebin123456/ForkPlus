using ForkPlus.Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 RemoteBranchSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/RemoteBranchSidebarItem.cs（55 行）：
    //   - WPF RemoteBranchSidebarItem : ReferenceSidebarItem
    //   - SidebarUserControl SidebarUserControl { get; }
    //   - RemoteBranch RemoteBranch { get; }
    //   - override string Tooltip => PreferencesLocalization.FormatCurrent("Remote branch '{0}'", RemoteBranch.ShortName)
    //   - override StartDrag/GetDataObject/GetDropEffect/Drop（拖拽 + 本地分支创建上下文菜单）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF PreferencesLocalization.FormatCurrent → ServiceLocator.Localization.FormatCurrent
    //   2. WPF SidebarUserControl（强类型）→ object（spike 替代具体依赖）
    //   3. WPF DependencyObject/DragEventArgs/DragDropEffects/DataObject → 省略拖拽逻辑
    //      （保留空方法 + 注释）
    //   4. WPF StartDrag 调用 DragDrop.DoDragDrop → spike 空实现
    //   5. WPF GetDataObject 构造 DataObject → spike 返回 null
    //   6. WPF GetDropEffect/Drop 含 LocalBranchSidebarItem 拖放 + ShowDropContextMenu → spike 省略
    //   7. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ReferenceSidebarItem
    //   - RemoteBranch + override Tooltip（ServiceLocator.Localization）
    //   - StartDrag/GetDataObject/GetDropEffect/Drop override 空实现
    public class RemoteBranchSidebarItem : ReferenceSidebarItem
    {
        // 对照 WPF: public SidebarUserControl SidebarUserControl { get; }
        // spike: WPF SidebarUserControl 强类型 → object
        public object SidebarUserControl { get; }

        // 对照 WPF: public RemoteBranch RemoteBranch { get; }
        public RemoteBranch RemoteBranch { get; }

        // 对照 WPF: public override string Tooltip => PreferencesLocalization.FormatCurrent("Remote branch '{0}'", RemoteBranch.ShortName);
        // spike: PreferencesLocalization.FormatCurrent → ServiceLocator.Localization.FormatCurrent
        public override string Tooltip => ServiceLocator.Localization.FormatCurrent("Remote branch '{0}'", RemoteBranch.ShortName);

        // 对照 WPF: public RemoteBranchSidebarItem(SidebarUserControl sidebarUserControl, string title, SidebarItem parent, RemoteBranch remoteBranch)
        public RemoteBranchSidebarItem(object sidebarUserControl, string title, SidebarItem parent, RemoteBranch remoteBranch)
            : base(title, parent, remoteBranch)
        {
            SidebarUserControl = sidebarUserControl;
            RemoteBranch = remoteBranch;
        }

        // 对照 WPF: public override void StartDrag(DependencyObject dragSource, MultiselectionTreeViewItem[] nodes)
        //   DragDrop.DoDragDrop(dragSource, GetDataObject(nodes), DragDropEffects.All);
        public override void StartDrag(object dragSource, MultiselectionTreeViewItem[] nodes)
        {
            // spike: WPF DragDrop.DoDragDrop 省略
        }

        // 对照 WPF: protected override IDataObject GetDataObject(MultiselectionTreeViewItem[] nodes)
        //   DataObject dataObject = new DataObject(); dataObject.SetData(SidebarItem.DragItemsFormat, nodes);
        protected override object GetDataObject(MultiselectionTreeViewItem[] nodes)
        {
            // spike: WPF DataObject 省略（返回 null）
            return null;
        }

        // 对照 WPF: public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
        //   检测 LocalBranchSidebarItem 拖入，返回 DragDropEffects.Move
        public override int GetDropEffect(object e, int index)
        {
            // spike: WPF 拖拽逻辑省略（返回 0 = DragDropEffects.None）
            return 0;
        }

        // 对照 WPF: public override void Drop(DragEventArgs e, int index)
        //   LocalBranchSidebarItem 拖入 → SidebarUserControl.ShowDropContextMenu(RemoteBranch, localBranch)
        public override void Drop(object e, int index)
        {
            // spike: WPF Drop 逻辑省略（依赖 SidebarUserControl.ShowDropContextMenu）
        }
    }
}
