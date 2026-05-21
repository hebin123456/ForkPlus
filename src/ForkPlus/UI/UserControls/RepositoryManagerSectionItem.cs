namespace ForkPlus.UI.UserControls
{
	public class RepositoryManagerSectionItem : RepositoryManagerTreeViewItem
	{
		public override bool IsFocusable => false;

		public RepositoryManagerSectionItem(RepositoryManagerTreeViewItem parent, string title)
			: base(parent)
		{
			base.Title = title;
		}
	}
}
