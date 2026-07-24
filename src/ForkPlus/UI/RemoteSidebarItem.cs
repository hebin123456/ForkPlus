// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Input
// - DragDropEffects / DragEventArgs → Avalonia.Input 同名类型
// - e.Effects → e.DragEffects
using Avalonia;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

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
			e.DragEffects = DragDropEffects.None;
			e.Handled = true;
		}
	}
}
