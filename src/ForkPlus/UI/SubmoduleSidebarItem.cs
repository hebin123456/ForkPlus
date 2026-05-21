using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class SubmoduleSidebarItem : SidebarItem
	{
		private bool _isDirty;

		public bool IsDirty
		{
			get
			{
				return _isDirty;
			}
			set
			{
				if (_isDirty != value)
				{
					_isDirty = value;
					base.Title = (_isDirty ? (SubmoduleName + "*") : SubmoduleName);
					RaisePropertyChanged("Title");
				}
			}
		}

		private string SubmoduleName { get; }

		public Submodule Submodule { get; }

		public SubmoduleSidebarItem(string title, SidebarItem parent, Submodule submodule)
			: base(title, parent)
		{
			Submodule = submodule;
			SubmoduleName = title;
		}
	}
}
