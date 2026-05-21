using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class WorktreeSidebarItem : SidebarItem
	{
		public Worktree Worktree { get; }

		public string Tooltip { get; }

		public WorktreeSidebarItem(string title, SidebarItem parent, Worktree worktree)
			: base(title, parent)
		{
			Worktree = worktree;
			Tooltip = worktree.GetTooltip();
		}
	}
}
