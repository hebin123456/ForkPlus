using System;
using ForkPlus.Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Services;

namespace ForkPlus.Avalonia
{
    // Avalonia 版 ReferenceSidebarItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/ReferenceSidebarItem.cs（76 行）：
    //   - WPF ReferenceSidebarItem : SidebarItem（abstract）
    //   - private bool _pinned + Pinned 属性
    //   - private ReferenceFilterState _filterState + FilterState 属性
    //   - Reference Reference { get; }
    //   - virtual string Tooltip { get; }
    //   - string PinTooltip => PreferencesLocalization.FormatCurrent("Pin '{0}'", base.Title)
    //   - string FilterTooltip => PreferencesLocalization.FormatCurrent("Show '{0}' commits only", base.Title)
    //   - string HideTooltip => PreferencesLocalization.FormatCurrent("Hide '{0}' in the commit list", base.Title)
    //   - virtual void ApplyLocalization()
    //   - override bool MatchFilter(string)（按 Reference.Name 匹配）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF PreferencesLocalization.Current/FormatCurrent → ServiceLocator.Localization.Current/FormatCurrent
    //   2. INotifyPropertyChanged 继承自 MultiselectionTreeViewItem（spike 已有）
    //   3. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
    //
    // spike 简化（task spec 关键 API）：
    //   - abstract 继承 SidebarItem
    //   - Reference + Pinned + FilterState
    //   - PinTooltip/FilterTooltip/HideTooltip（ServiceLocator.Localization）
    //   - ApplyLocalization + MatchFilter override
    public abstract class ReferenceSidebarItem : SidebarItem
    {
        // 对照 WPF: private bool _pinned;
        private bool _pinned;

        // 对照 WPF: private ReferenceFilterState _filterState;
        private ReferenceFilterState _filterState;

        // 对照 WPF: public Reference Reference { get; }
        public Reference Reference { get; }

        // 对照 WPF: public virtual string Tooltip { get; }
        public virtual string Tooltip { get; }

        // 对照 WPF: public string PinTooltip => PreferencesLocalization.FormatCurrent("Pin '{0}'", base.Title);
        // spike: PreferencesLocalization → ServiceLocator.Localization
        public string PinTooltip => ServiceLocator.Localization.FormatCurrent("Pin '{0}'", Title);

        // 对照 WPF: public string FilterTooltip => PreferencesLocalization.FormatCurrent("Show '{0}' commits only", base.Title);
        public string FilterTooltip => ServiceLocator.Localization.FormatCurrent("Show '{0}' commits only", Title);

        // 对照 WPF: public string HideTooltip => PreferencesLocalization.FormatCurrent("Hide '{0}' in the commit list", base.Title);
        public string HideTooltip => ServiceLocator.Localization.FormatCurrent("Hide '{0}' in the commit list", Title);

        // 对照 WPF: public bool Pinned { get { return _pinned; } set { _pinned = value; RaisePropertyChanged("Pinned"); } }
        public bool Pinned
        {
            get => _pinned;
            set
            {
                _pinned = value;
                RaisePropertyChanged(nameof(Pinned));
            }
        }

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

        // 对照 WPF: public ReferenceSidebarItem(string title, SidebarItem parent, Reference reference)
        public ReferenceSidebarItem(string title, SidebarItem parent, Reference reference)
            : base(title, parent)
        {
            Reference = reference;
        }

        // 对照 WPF: public virtual void ApplyLocalization()
        public virtual void ApplyLocalization()
        {
            RaisePropertyChanged(nameof(Tooltip));
            RaisePropertyChanged(nameof(PinTooltip));
            RaisePropertyChanged(nameof(FilterTooltip));
            RaisePropertyChanged(nameof(HideTooltip));
        }

        // 对照 WPF: protected override bool MatchFilter(string filterString)
        //   按 Reference.Name 匹配（基类按 Title 匹配）
        protected override bool MatchFilter(string filterString)
        {
            if (string.IsNullOrEmpty(filterString))
            {
                return true;
            }
            if (Reference.Name.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) != -1)
            {
                return true;
            }
            return false;
        }
    }
}
