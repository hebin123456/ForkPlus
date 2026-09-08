using Avalonia;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Input;

namespace ForkPlus.UI
{
	public class RemoteSidebarItem : FolderSidebarItem
	{
		public Remote Remote { get; }

		public string Tooltip { get; }

		public RemoteSidebarItem(string title, SidebarItem parent, Remote remote, SidebarUserControl sidebarUserControl)
			: base(title, parent, sidebarUserControl)
		{
			Remote = remote;
			Tooltip = remote.Url;
		}

		public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
		{
			e.Handled = true;
			return DragDropEffects.None;
		}

		public override void Drop(DragEventArgs e, int index)
		{
			e.DragEffects= DragDropEffects.None;
			e.Handled = true;
		}
	}
}
