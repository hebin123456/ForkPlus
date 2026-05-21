using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI
{
	public class MainWorktreeSidebarItem : FolderSidebarItem
	{
		public Worktree Worktree { get; }

		public string Tooltip { get; }

		public MainWorktreeSidebarItem(string title, SidebarItem parent, Worktree worktree, SidebarUserControl sidebarUserControl)
			: base(title, parent, sidebarUserControl)
		{
			Worktree = worktree;
			Tooltip = worktree.GetTooltip();
		}
	}
}
