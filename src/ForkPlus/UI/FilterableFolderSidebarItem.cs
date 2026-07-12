using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	public class FilterableFolderSidebarItem : FolderSidebarItem
	{
		private ReferenceFilterState _filterState;

		public string FilterTooltip => PreferencesLocalization.FormatCurrent("Show '{0}' commits only", base.Title);

		public string HideTooltip => PreferencesLocalization.FormatCurrent("Hide '{0}' in the commit list", base.Title);

		public ReferenceFilterState FilterState
		{
			get
			{
				return _filterState;
			}
			set
			{
				_filterState = value;
				RaisePropertyChanged("FilterState");
			}
		}

		[Null]
		public string FullReference
		{
			get
			{
				if (base.Parent is FilterableRemoteSidebarItem filterableRemoteSidebarItem)
				{
					return filterableRemoteSidebarItem.FullReference + base.Title + "/";
				}
				if (base.Parent is FilterableFolderSidebarItem filterableFolderSidebarItem)
				{
					return filterableFolderSidebarItem.FullReference + base.Title + "/";
				}
				if (base.Parent is SidebarGroupItem sidebarGroupItem)
				{
					if (sidebarGroupItem.GroupType == SidebarGroupItem.Group.Branches)
					{
						return "refs/heads/" + base.Title + "/";
					}
					if (sidebarGroupItem.GroupType == SidebarGroupItem.Group.Tags)
					{
						return "refs/tags/" + base.Title + "/";
					}
				}
				return null;
			}
		}

		public FilterableFolderSidebarItem(string title, SidebarItem parent, SidebarUserControl sidebarUserControl)
			: base(title, parent, sidebarUserControl)
		{
		}

		public void ApplyLocalization()
		{
			RaisePropertyChanged(nameof(FilterTooltip));
			RaisePropertyChanged(nameof(HideTooltip));
		}

	}
}
