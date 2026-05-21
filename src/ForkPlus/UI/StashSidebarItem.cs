using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class StashSidebarItem : SidebarItem
	{
		public StashRevision Stash { get; }

		public string Tooltip { get; }

		public StashSidebarItem(string title, SidebarItem parent, StashRevision stash)
			: base(title, parent)
		{
			Stash = stash;
			Tooltip = stash.Message;
		}
	}
}
