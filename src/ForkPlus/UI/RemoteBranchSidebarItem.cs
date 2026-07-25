// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Input
// - DependencyObject → AvaloniaObject（StartDrag 参数，与已迁移基类一致）
// - DragDrop.DoDragDrop → _ = DragDrop.DoDragDrop（异步返回 Task，丢弃；参考 LocalBranchSidebarItem/ClosableTabItem）
// - DataObject.SetData → DataObject.Set（Avalonia 11.3 方法名）
// - e.Data.GetData → e.Data.Get（Avalonia IDataObject 方法名）
// - e.Effects → e.DragEffects
using Avalonia;
using Avalonia.Input;
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

		public override void StartDrag(AvaloniaObject dragSource, MultiselectionTreeViewItem[] nodes)
		{
			// 阶段 5：WPF DragDrop.DoDragDrop → DragDropBridge.DoDragDrop（桥接 PointerEventArgs 来源）。
			_ = DragDropBridge.DoDragDrop(dragSource, GetDataObject(nodes), (DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link));
		}

		protected override IDataObject GetDataObject(MultiselectionTreeViewItem[] nodes)
		{
			DataObject dataObject = new DataObject();
			dataObject.Set(SidebarItem.DragItemsFormat, nodes);
			return dataObject;
		}

		public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
		{
			if (e.Data.Get(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source && source.SingleItem() is LocalBranchSidebarItem)
			{
				return DragDropEffects.Move;
			}
			return DragDropEffects.None;
		}

		public override void Drop(DragEventArgs e, int index)
		{
			e.DragEffects = DragDropEffects.None;
			if (e.Data.Get(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source && source.SingleItem() is LocalBranchSidebarItem localBranchSidebarItem)
			{
				e.Handled = true;
				e.DragEffects = DragDropEffects.Move;
				SidebarUserControl.ShowDropContextMenu(RemoteBranch, localBranchSidebarItem.LocalBranch);
			}
		}
	}
}
