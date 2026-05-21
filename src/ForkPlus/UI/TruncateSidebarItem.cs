namespace ForkPlus.UI
{
	public class TruncateSidebarItem : SidebarItem
	{
		public override bool IsFocusable => false;

		public TruncateSidebarItem(string title, SidebarItem parent)
			: base(title, parent)
		{
		}
	}
}
