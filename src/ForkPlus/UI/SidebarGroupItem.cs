using System.Windows;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI
{
	public class SidebarGroupItem : FolderSidebarItem
	{
		public enum Group
		{
			Pinned,
			Branches,
			Remotes,
			Tags,
			Stashes,
			Submodules,
			Worktrees
		}

		public Group GroupType { get; }

		public SidebarGroupItem(string title, SidebarItem parent, Group group, SidebarUserControl sidebarUserControl)
			: base(title, parent, sidebarUserControl)
		{
			GroupType = group;
		}

		public void RefreshTitle()
		{
			Title = UserControls.Preferences.PreferencesLocalization.Current(GroupType.ToString());
			RaisePropertyChanged(nameof(Title));
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
