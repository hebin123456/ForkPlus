// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Input
// - DependencyObject → AvaloniaObject（StartDrag 参数，与已迁移基类 MultiselectionTreeViewItem 一致）
// - DragDrop.DoDragDrop → _ = DragDrop.DoDragDrop（异步返回 Task<DragDropEffects>，丢弃；参考 ClosableTabItem）
// - DataObject.SetData → DataObject.Set（Avalonia 11.3 DataObject 方法名为 Set，非 SetData）
// - e.Data.GetData → e.Data.Get（Avalonia IDataObject 方法名为 Get，非 GetData）
// - e.Effects → e.DragEffects（Avalonia DragEventArgs 属性名）
using System;
using Avalonia;
using Avalonia.Input;
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

		public override void StartDrag(AvaloniaObject dragSource, MultiselectionTreeViewItem[] nodes)
		{
			_ = DragDrop.DoDragDrop(dragSource, GetDataObject(nodes), DragDropEffects.All);
		}

		protected override IDataObject GetDataObject(MultiselectionTreeViewItem[] nodes)
		{
			DataObject dataObject = new DataObject();
			dataObject.Set(SidebarItem.DragItemsFormat, nodes);
			return dataObject;
		}

		public override DragDropEffects GetDropEffect(DragEventArgs e, int index)
		{
			if (e.Data.Get(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source && source.SingleItem() is ReferenceSidebarItem)
			{
				return DragDropEffects.Move;
			}
			return DragDropEffects.None;
		}

		public override void Drop(DragEventArgs e, int index)
		{
			e.DragEffects = DragDropEffects.None;
			if (e.Data.Get(SidebarItem.DragItemsFormat) is MultiselectionTreeViewItem[] source && source.SingleItem() is ReferenceSidebarItem { Reference: Branch reference } && reference != LocalBranch)
			{
				e.Handled = true;
				e.DragEffects = DragDropEffects.Move;
				SidebarUserControl.ShowDropContextMenu(LocalBranch, reference);
			}
		}
	}
}
