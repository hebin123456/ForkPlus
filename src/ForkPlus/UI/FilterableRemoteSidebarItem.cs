using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	public class FilterableRemoteSidebarItem : RemoteSidebarItem
	{
		private ReferenceFilterState _filterState;

		public string FilterTooltip => PreferencesLocalization.Current("Show '" + base.Title + "' commits only");

		public string HideTooltip => PreferencesLocalization.Current("Hide '" + base.Title + "' in the commit list");

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

		public string FullReference => "refs/remotes/" + base.Remote.Name + "/";

		public FilterableRemoteSidebarItem(string title, SidebarItem parent, Remote remote, SidebarUserControl sidebarUserControl)
			: base(title, parent, remote, sidebarUserControl)
		{
		}

		public void ApplyLocalization()
		{
			RaisePropertyChanged(nameof(FilterTooltip));
			RaisePropertyChanged(nameof(HideTooltip));
		}

	}
}
