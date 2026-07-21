using System.Windows;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI
{
	public class RemoteSidebarItem : FolderSidebarItem
	{
		public Remote Remote { get; }

		public string Tooltip { get; }

		/// <summary>
		/// 供 Sidebar.xaml DataTemplate 绑定（{Binding Path="RemoteIcon"}）使用。
		/// 原来绑定 Remote.Icon（WPF partial 提供的 ImageSource 属性），
		/// Phase 0.2c 删除 WPF 端 Remote.partial.cs 后改用 RemoteBridgeExtensions.GetIconImage 扩展方法。
		/// </summary>
		public ImageSource RemoteIcon => Remote.GetIconImage();

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
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}
	}
}
