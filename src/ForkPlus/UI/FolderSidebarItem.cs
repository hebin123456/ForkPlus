using System.Windows;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI
{
	public class FolderSidebarItem : SidebarItem
	{
		public SidebarUserControl SidebarUserControl { get; }

		private bool IsRoot => base.ParentItem == null;

		private string FullName
		{
			get
			{
				string text = base.Title;
				MultiselectionTreeViewItem parentItem = base.ParentItem;
				while (true)
				{
					FolderSidebarItem folderSidebarItem = AsBranchFolder(parentItem);
					if (folderSidebarItem == null)
					{
						break;
					}
					text = folderSidebarItem.Title + "/" + text;
					parentItem = folderSidebarItem.ParentItem;
				}
				return text;
			}
		}

		public FolderSidebarItem(string title, SidebarItem parent, SidebarUserControl sidebarUserControl)
			: base(title, parent)
		{
			SidebarUserControl = sidebarUserControl;
		}

		protected override void OnExpanding()
		{
			base.OnExpanding();
			SidebarUserControl.OnDirectoryItemIsExpandedChanged();
		}

		protected override void OnCollapsing()
		{
			base.OnCollapsing();
			SidebarUserControl.OnDirectoryItemIsExpandedChanged();
		}

		public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
		{
			if (e.Data.GetData(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source && source.SingleItem() is LocalBranchSidebarItem && !IsRoot)
			{
				return DragDropEffects.Move;
			}
			return DragDropEffects.None;
		}

		public override void Drop(DragEventArgs e, int index)
		{
			e.Effects = DragDropEffects.None;
			if (!(e.Data.GetData(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source))
			{
				return;
			}
			MultiselectionTreeViewItem multiselectionTreeViewItem = source.SingleItem();
			LocalBranchSidebarItem localBranchSidebarItem = multiselectionTreeViewItem as LocalBranchSidebarItem;
			if (localBranchSidebarItem == null || IsRoot)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = SidebarUserControl.RepositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = SidebarUserControl.RepositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = SidebarUserControl.RepositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				e.Handled = true;
				e.Effects = DragDropEffects.Move;
				string newName = FullName + "/" + localBranchSidebarItem.LocalBranch.LastNameComponent();
				SidebarUserControl.Dispatcher.Async(delegate
				{
					RepositoryUserControl.Commands.ShowRenameLocalBranchWindow.Execute(repositoryUserControl, gitModule, repositoryData.References, localBranchSidebarItem.LocalBranch, newName);
				});
			}
		}

		[Null]
		private FolderSidebarItem AsBranchFolder(MultiselectionTreeViewItem item)
		{
			if (!(item is FolderSidebarItem folderSidebarItem))
			{
				return null;
			}
			if (folderSidebarItem is SidebarGroupItem)
			{
				return null;
			}
			if (folderSidebarItem is RemoteSidebarItem)
			{
				return null;
			}
			return folderSidebarItem;
		}
	}
}
