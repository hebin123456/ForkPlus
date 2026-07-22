using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 FilterableRemoteSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/FilterableRemoteSidebarItem.cs（41 行）：
    //   - WPF FilterableRemoteSidebarItem : RemoteSidebarItem
    //   - private ReferenceFilterState _filterState + FilterState 属性
    //   - string FilterTooltip => PreferencesLocalization.Current("Show '" + base.Title + "' commits only")
    //   - string HideTooltip => PreferencesLocalization.Current("Hide '" + base.Title + "' in the commit list")
    //   - string FullReference => "refs/remotes/" + base.Remote.Name + "/"
    //   - void ApplyLocalization()
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF PreferencesLocalization.Current → ServiceLocator.Localization.Current
    //   2. WPF SidebarUserControl（强类型）→ object（透传基类，spike 替代）
    //   3. INotifyPropertyChanged 继承自 MultiselectionTreeViewItem（spike 已有）
    //   4. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 RemoteSidebarItem
    //   - FilterState + FilterTooltip/HideTooltip（ServiceLocator.Localization）
    //   - FullReference（refs/remotes/ 前缀）
    public class FilterableRemoteSidebarItem : RemoteSidebarItem
    {
        // 对照 WPF: private ReferenceFilterState _filterState;
        private ReferenceFilterState _filterState;

        // 对照 WPF: public string FilterTooltip => PreferencesLocalization.Current("Show '" + base.Title + "' commits only");
        // spike: PreferencesLocalization.Current → ServiceLocator.Localization.Current
        public string FilterTooltip => ServiceLocator.Localization.Current("Show '" + Title + "' commits only");

        // 对照 WPF: public string HideTooltip => PreferencesLocalization.Current("Hide '" + base.Title + "' in the commit list");
        public string HideTooltip => ServiceLocator.Localization.Current("Hide '" + Title + "' in the commit list");

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

        // 对照 WPF: public string FullReference => "refs/remotes/" + base.Remote.Name + "/";
        public string FullReference => "refs/remotes/" + Remote.Name + "/";

        // 对照 WPF: public FilterableRemoteSidebarItem(string title, SidebarItem parent, Remote remote, SidebarUserControl sidebarUserControl)
        public FilterableRemoteSidebarItem(string title, SidebarItem parent, Remote remote, object sidebarUserControl)
            : base(title, parent, remote, sidebarUserControl)
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
