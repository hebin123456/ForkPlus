using System;
using System.Windows;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	public class LocalBranchSidebarItem : ReferenceSidebarItem
	{
		public UpstreamStatus? _upstreamStatus;

		public SidebarUserControl SidebarUserControl { get; }

		public LocalBranch LocalBranch { get; }

		public UpstreamStatus? UpstreamStatus
		{
			get
			{
				return _upstreamStatus;
			}
			set
			{
				if (!_upstreamStatus.Equals(value))
				{
					_upstreamStatus = value;
					UpstreamStatusString = value?.ToShortDescription() ?? "";
					RaisePropertyChanged("UpstreamStatus");
					RaisePropertyChanged("UpstreamStatusString");
					RaisePropertyChanged("Tooltip");
				}
			}
		}

		public string UpstreamStatusString { get; private set; }

		public override string Tooltip => GetTooltip(LocalBranch, UpstreamStatus);

		public LocalBranchSidebarItem(SidebarUserControl sidebarUserControl, string title, SidebarItem parent, LocalBranch localBranch)
			: base(title, parent, localBranch)
		{
			SidebarUserControl = sidebarUserControl;
			LocalBranch = localBranch;
		}

		private static string GetTooltip(LocalBranch localBranch, UpstreamStatus? upstreamStatus)
		{
			if (upstreamStatus.HasValue)
			{
				if (upstreamStatus.GetValueOrDefault().IsValid)
				{
					return PreferencesLocalization.Current("Local branch:") + "\t" + localBranch.Name + Environment.NewLine + PreferencesLocalization.Current("Tracked branch:") + "\t" + localBranch.UpstreamFullName;
				}
				return PreferencesLocalization.Current("Local branch:") + "\t" + localBranch.Name + Environment.NewLine + PreferencesLocalization.Current("Tracked branch:") + "\t" + localBranch.UpstreamFullName + " " + PreferencesLocalization.Current("[removed]");
			}
			return PreferencesLocalization.Current("Local branch:") + "\t" + localBranch.Name;
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
			if (e.Data.GetData(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source && source.SingleItem() is ReferenceSidebarItem)
			{
				return DragDropEffects.Move;
			}
			return DragDropEffects.None;
		}

		public override void Drop(DragEventArgs e, int index)
		{
			e.Effects = DragDropEffects.None;
			if (e.Data.GetData(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source && source.SingleItem() is ReferenceSidebarItem { Reference: Branch reference } && reference != LocalBranch)
			{
				e.Handled = true;
				e.Effects = DragDropEffects.Move;
				SidebarUserControl.ShowDropContextMenu(LocalBranch, reference);
			}
		}
	}
}
