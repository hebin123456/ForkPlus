using System;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	public abstract class ReferenceSidebarItem : SidebarItem
	{
		private bool _pinned;

		private ReferenceFilterState _filterState;

		public Reference Reference { get; }

		public virtual string Tooltip { get; }

		public string PinTooltip => PreferencesLocalization.Current("Pin '" + base.Title + "'");

		public string FilterTooltip => PreferencesLocalization.Current("Show '" + base.Title + "' commits only");

		public string HideTooltip => PreferencesLocalization.Current("Hide '" + base.Title + "' in the commit list");

		public bool Pinned
		{
			get
			{
				return _pinned;
			}
			set
			{
				_pinned = value;
				RaisePropertyChanged("Pinned");
			}
		}

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

		public ReferenceSidebarItem(string title, SidebarItem parent, Reference reference)
			: base(title, parent)
		{
			Reference = reference;
		}

		public virtual void ApplyLocalization()
		{
			RaisePropertyChanged(nameof(Tooltip));
			RaisePropertyChanged(nameof(PinTooltip));
			RaisePropertyChanged(nameof(FilterTooltip));
			RaisePropertyChanged(nameof(HideTooltip));
		}

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
