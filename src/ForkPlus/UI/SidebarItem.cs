using ForkPlus.UI.Controls;

namespace ForkPlus.UI
{
	public abstract class SidebarItem : MultiselectionTreeViewItem
	{
		public static readonly string DragItemsFormat = "SidebarTreeView";

		public SidebarItem Parent { get; }

		public override bool IsFocusable => !ShowExpander;

		public SidebarItem(string title, SidebarItem parent)
		{
			base.Title = title;
			Parent = parent;
		}
	}
}
