using System;
using ForkPlus.Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 LocalBranchSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/LocalBranchSidebarItem.cs（92 行）：
    //   - WPF LocalBranchSidebarItem : ReferenceSidebarItem
    //   - public UpstreamStatus? _upstreamStatus（字段，WPF public）
    //   - SidebarUserControl SidebarUserControl { get; }
    //   - LocalBranch LocalBranch { get; }
    //   - UpstreamStatus? UpstreamStatus（set 时更新 UpstreamStatusString + Tooltip）
    //   - string UpstreamStatusString { get; private set; }
    //   - override string Tooltip => GetTooltip(LocalBranch, UpstreamStatus)
    //   - private static string GetTooltip(LocalBranch, UpstreamStatus?)
    //   - override StartDrag/GetDataObject/GetDropEffect/Drop（拖拽 + 分支切换/重命名上下文菜单）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF PreferencesLocalization.Current → ServiceLocator.Localization.Current
    //   2. WPF SidebarUserControl（强类型）→ object（spike 替代具体依赖）
    //   3. WPF DependencyObject/DragEventArgs/DragDropEffects/DataObject → 省略拖拽逻辑
    //      （保留空方法 + 注释）
    //   4. WPF StartDrag 调用 DragDrop.DoDragDrop → spike 空实现
    //   5. WPF GetDataObject 构造 DataObject → spike 返回 null
    //   6. WPF GetDropEffect/Drop 含 ReferenceSidebarItem 拖放 + ShowDropContextMenu → spike 省略
    //   7. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ReferenceSidebarItem
    //   - LocalBranch + UpstreamStatus + UpstreamStatusString
    //   - override Tooltip（GetTooltip 用 ServiceLocator.Localization）
    //   - StartDrag/GetDataObject/GetDropEffect/Drop override 空实现
    public class LocalBranchSidebarItem : ReferenceSidebarItem
    {
        // 对照 WPF: public UpstreamStatus? _upstreamStatus;
        public UpstreamStatus? _upstreamStatus;

        // 对照 WPF: public SidebarUserControl SidebarUserControl { get; }
        // spike: WPF SidebarUserControl 强类型 → object
        public object SidebarUserControl { get; }

        // 对照 WPF: public LocalBranch LocalBranch { get; }
        public LocalBranch LocalBranch { get; }

        // 对照 WPF: public UpstreamStatus? UpstreamStatus
        //   set 时 _upstreamStatus.Equals(value) 变化检测，更新 UpstreamStatusString + Tooltip
        public UpstreamStatus? UpstreamStatus
        {
            get => _upstreamStatus;
            set
            {
                if (!_upstreamStatus.Equals(value))
                {
                    _upstreamStatus = value;
                    UpstreamStatusString = value?.ToShortDescription() ?? "";
                    RaisePropertyChanged(nameof(UpstreamStatus));
                    RaisePropertyChanged(nameof(UpstreamStatusString));
                    RaisePropertyChanged(nameof(Tooltip));
                }
            }
        }

        // 对照 WPF: public string UpstreamStatusString { get; private set; }
        public string UpstreamStatusString { get; private set; }

        // 对照 WPF: public override string Tooltip => GetTooltip(LocalBranch, UpstreamStatus);
        public override string Tooltip => GetTooltip(LocalBranch, UpstreamStatus);

        // 对照 WPF: public LocalBranchSidebarItem(SidebarUserControl sidebarUserControl, string title, SidebarItem parent, LocalBranch localBranch)
        public LocalBranchSidebarItem(object sidebarUserControl, string title, SidebarItem parent, LocalBranch localBranch)
            : base(title, parent, localBranch)
        {
            SidebarUserControl = sidebarUserControl;
            LocalBranch = localBranch;
        }

        // 对照 WPF: private static string GetTooltip(LocalBranch localBranch, UpstreamStatus? upstreamStatus)
        //   spike: PreferencesLocalization.Current → ServiceLocator.Localization.Current
        private static string GetTooltip(LocalBranch localBranch, UpstreamStatus? upstreamStatus)
        {
            if (upstreamStatus.HasValue)
            {
                if (upstreamStatus.GetValueOrDefault().IsValid)
                {
                    return ServiceLocator.Localization.Current("Local branch:") + "\t" + localBranch.Name
                        + Environment.NewLine + ServiceLocator.Localization.Current("Tracked branch:") + "\t" + localBranch.UpstreamFullName;
                }
                return ServiceLocator.Localization.Current("Local branch:") + "\t" + localBranch.Name
                    + Environment.NewLine + ServiceLocator.Localization.Current("Tracked branch:") + "\t" + localBranch.UpstreamFullName
                    + " " + ServiceLocator.Localization.Current("[removed]");
            }
            return ServiceLocator.Localization.Current("Local branch:") + "\t" + localBranch.Name;
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
        //   检测 ReferenceSidebarItem 拖入，返回 DragDropEffects.Move
        public override int GetDropEffect(object e, int index)
        {
            // spike: WPF 拖拽逻辑省略（返回 0 = DragDropEffects.None）
            return 0;
        }

        // 对照 WPF: public override void Drop(DragEventArgs e, int index)
        //   ReferenceSidebarItem { Reference: Branch } 拖入 → SidebarUserControl.ShowDropContextMenu
        public override void Drop(object e, int index)
        {
            // spike: WPF Drop 逻辑省略（依赖 SidebarUserControl.ShowDropContextMenu）
        }
    }
}
