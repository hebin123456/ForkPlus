using ForkPlus.Avalonia.Controls;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 FolderSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/FolderSidebarItem.cs（114 行）：
    //   - WPF FolderSidebarItem : SidebarItem
    //   - SidebarUserControl SidebarUserControl { get; }（WPF 强类型，依赖 RepositoryUserControl）
    //   - private bool IsRoot => base.ParentItem == null
    //   - private string FullName（沿 ParentItem 链拼接分支文件夹路径）
    //   - override OnExpanding/OnCollapsing → SidebarUserControl.OnDirectoryItemIsExpandedChanged()
    //   - override GetDropEffect/Drop（LocalBranch 拖入文件夹重命名逻辑）
    //   - private AsBranchFolder（过滤 SidebarGroupItem/RemoteSidebarItem）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF SidebarUserControl（强类型）→ object（spike 替代具体依赖）
    //   2. OnExpanding/OnCollapsing 中 SidebarUserControl.OnDirectoryItemIsExpandedChanged() 省略
    //      （依赖 SidebarUserControl 具体类型，spike 用 object 无法调用）
    //   3. WPF DragEventArgs/DragDropEffects/DataObject → 省略拖拽逻辑（保留空方法 + 注释）
    //   4. WPF GetDropEffect/Drop 含 LocalBranchSidebarItem 拖入文件夹重命名逻辑（依赖
    //      RepositoryUserControl.Commands.ShowRenameLocalBranchWindow），spike 全部省略
    //   5. private FullName / AsBranchFolder（仅被 Drop 使用）spike 省略
    //   6. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 SidebarItem
    //   - SidebarUserControl（object 替代）
    //   - IsRoot 计算属性
    //   - OnExpanding/OnCollapsing override（调用 base，省略 SidebarUserControl 调用）
    //   - GetDropEffect/Drop override 空实现
    public class FolderSidebarItem : SidebarItem
    {
        // 对照 WPF: public SidebarUserControl SidebarUserControl { get; }
        // spike: WPF SidebarUserControl 强类型 → object
        public object SidebarUserControl { get; }

        // 对照 WPF: private bool IsRoot => base.ParentItem == null;
        private bool IsRoot => ParentItem == null;

        // 对照 WPF: public FolderSidebarItem(string title, SidebarItem parent, SidebarUserControl sidebarUserControl)
        public FolderSidebarItem(string title, SidebarItem parent, object sidebarUserControl)
            : base(title, parent)
        {
            SidebarUserControl = sidebarUserControl;
        }

        // 对照 WPF: protected override void OnExpanding()
        //   base.OnExpanding(); SidebarUserControl.OnDirectoryItemIsExpandedChanged();
        protected override void OnExpanding()
        {
            base.OnExpanding();
            // spike: SidebarUserControl.OnDirectoryItemIsExpandedChanged() 省略
            // （依赖 SidebarUserControl 具体类型，spike 用 object 替代无法调用）
        }

        // 对照 WPF: protected override void OnCollapsing()
        //   base.OnCollapsing(); SidebarUserControl.OnDirectoryItemIsExpandedChanged();
        protected override void OnCollapsing()
        {
            base.OnCollapsing();
            // spike: SidebarUserControl.OnDirectoryItemIsExpandedChanged() 省略
        }

        // spike: 拖拽逻辑省略（WPF DragDrop/DragEventArgs 依赖未迁移）
        // 对照 WPF: public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
        //   检测 LocalBranchSidebarItem 拖入非根文件夹，返回 DragDropEffects.Move
        public override int GetDropEffect(object e, int index)
        {
            // spike: WPF 拖拽逻辑省略（返回 0 = DragDropEffects.None）
            return 0;
        }

        // 对照 WPF: public override void Drop(DragEventArgs e, int index)
        //   LocalBranchSidebarItem 拖入文件夹 → ShowRenameLocalBranchWindow 重命名分支
        public override void Drop(object e, int index)
        {
            // spike: WPF Drop 逻辑省略（依赖 RepositoryUserControl.Commands + GitModule）
        }
    }
}
