using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 FilterableFolderSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/FilterableFolderSidebarItem.cs（67 行）：
    //   - WPF FilterableFolderSidebarItem : FolderSidebarItem
    //   - private ReferenceFilterState _filterState + FilterState 属性
    //   - string FilterTooltip => PreferencesLocalization.FormatCurrent("Show '{0}' commits only", base.Title)
    //   - string HideTooltip => PreferencesLocalization.FormatCurrent("Hide '{0}' in the commit list", base.Title)
    //   - [Null] string FullReference（沿 Parent 链拼接 refs/heads/ refs/tags/ refs/remotes/ 前缀）
    //   - void ApplyLocalization()
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF PreferencesLocalization.FormatCurrent → ServiceLocator.Localization.FormatCurrent
    //   2. WPF [Null] Attribute → spike 跳过（nullable disable in csproj）
    //   3. WPF SidebarUserControl（强类型）→ object（透传基类，spike 替代）
    //   4. INotifyPropertyChanged 继承自 MultiselectionTreeViewItem（spike 已有）
    //   5. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 FolderSidebarItem
    //   - FilterState + FilterTooltip/HideTooltip（ServiceLocator.Localization）
    //   - FullReference（沿 Parent 链拼接引用前缀）
    public class FilterableFolderSidebarItem : FolderSidebarItem
    {
        // 对照 WPF: private ReferenceFilterState _filterState;
        private ReferenceFilterState _filterState;

        // 对照 WPF: public string FilterTooltip => PreferencesLocalization.FormatCurrent("Show '{0}' commits only", base.Title);
        // spike: PreferencesLocalization.FormatCurrent → ServiceLocator.Localization.FormatCurrent
        public string FilterTooltip => ServiceLocator.Localization.FormatCurrent("Show '{0}' commits only", Title);

        // 对照 WPF: public string HideTooltip => PreferencesLocalization.FormatCurrent("Hide '{0}' in the commit list", base.Title);
        public string HideTooltip => ServiceLocator.Localization.FormatCurrent("Hide '{0}' in the commit list", Title);

        // 对照 WPF: public ReferenceFilterState FilterState { get { return _filterState; } set { _filterState = value; RaisePropertyChanged("FilterState"); } }
        public ReferenceFilterState FilterState
        {
            get => _filterState;
            set
            {
                _filterState = value;
                RaisePropertyChanged(nameof(FilterState));
            }
        }

        // 对照 WPF: [Null] public string FullReference
        //   按 Parent 类型（FilterableRemoteSidebarItem / FilterableFolderSidebarItem / SidebarGroupItem）
        //   拼接 refs/heads/ refs/tags/ 前缀的完整引用路径
        // spike: [Null] Attribute 跳过（nullable disable）
        public string FullReference
        {
            get
            {
                if (Parent is FilterableRemoteSidebarItem filterableRemoteSidebarItem)
                {
                    return filterableRemoteSidebarItem.FullReference + Title + "/";
                }
                if (Parent is FilterableFolderSidebarItem filterableFolderSidebarItem)
                {
                    return filterableFolderSidebarItem.FullReference + Title + "/";
                }
                if (Parent is SidebarGroupItem sidebarGroupItem)
                {
                    if (sidebarGroupItem.GroupType == SidebarGroupItem.Group.Branches)
                    {
                        return "refs/heads/" + Title + "/";
                    }
                    if (sidebarGroupItem.GroupType == SidebarGroupItem.Group.Tags)
                    {
                        return "refs/tags/" + Title + "/";
                    }
                }
                return null;
            }
        }

        // 对照 WPF: public FilterableFolderSidebarItem(string title, SidebarItem parent, SidebarUserControl sidebarUserControl)
        public FilterableFolderSidebarItem(string title, SidebarItem parent, object sidebarUserControl)
            : base(title, parent, sidebarUserControl)
        {
        }

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            RaisePropertyChanged(nameof(FilterTooltip));
            RaisePropertyChanged(nameof(HideTooltip));
        }
    }
}
