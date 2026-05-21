using System.Windows;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	public class RemoteBranchSidebarItem : ReferenceSidebarItem
	{
		public SidebarUserControl SidebarUserControl { get; }

		public RemoteBranch RemoteBranch { get; }

		public override string Tooltip => PreferencesLocalization.FormatCurrent("Remote branch '{0}'", RemoteBranch.ShortName);

		public RemoteBranchSidebarItem(SidebarUserControl sidebarUserControl, string title, SidebarItem parent, RemoteBranch remoteBranch)
			: base(title, parent, remoteBranch)
		{
			SidebarUserControl = sidebarUserControl;
			RemoteBranch = remoteBranch;
		}

		public override void StartDrag(DependencyObject dragSource, MultiselectionTreeViewItem[] nodes)
		{
			DragDrop.DoDragDrop(dragSource, GetDataObject(nodes), DragDropEffects.All);
		}

		protected override IDataObject GetDataObject(MultiselectionTreeViewItem[] nodes)
		{
			DataObject dataObject = new DataObject();
			dataObject.SetData(SidebarItem.DragItemsFormat, nodes);
			return dataObject;
		}

		public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
		{
			if (e.Data.GetData(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source && source.SingleItem() is LocalBranchSidebarItem)
			{
				return DragDropEffects.Move;
			}
			return DragDropEffects.None;
		}

		public override void Drop(DragEventArgs e, int index)
		{
			e.Effects = DragDropEffects.None;
			if (e.Data.GetData(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source && source.SingleItem() is LocalBranchSidebarItem localBranchSidebarItem)
			{
				e.Handled = true;
				e.Effects = DragDropEffects.Move;
				SidebarUserControl.ShowDropContextMenu(RemoteBranch, localBranchSidebarItem.LocalBranch);
			}
		}
	}
}
