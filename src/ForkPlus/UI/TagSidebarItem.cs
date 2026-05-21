using System;
using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class TagSidebarItem : ReferenceSidebarItem
	{
		private Tag Tag => base.Reference as Tag;

		public override string Tooltip => $"Tag '{Tag.Name}'{Environment.NewLine}{Tag.CommitterDate}";

		public TagSidebarItem(string title, SidebarItem parent, Tag tag)
			: base(title, parent, tag)
		{
		}
	}
}
