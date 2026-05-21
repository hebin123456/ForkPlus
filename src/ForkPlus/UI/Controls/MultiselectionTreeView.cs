using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ForkPlus.UI.Controls.Flattener;

namespace ForkPlus.UI.Controls
{
	public class MultiselectionTreeView : ListView
	{
		private class DropTarget
		{
			public TreeViewControlItem Item;

			public double Y;

			public MultiselectionTreeViewItem Node;

			public int Index;

			public DragDropEffects Effect;
		}

		private class UpdateLock : IDisposable
		{
			private MultiselectionTreeView _instance;

			public UpdateLock(MultiselectionTreeView instance)
			{
				_instance = instance;
				_instance._updatesLocked = true;
			}

			public void Dispose()
			{
				_instance._updatesLocked = false;
			}
		}

		public static readonly DependencyProperty RootItemProperty;

		[Null]
		private ExpandedTreeViewElement[] _itemsToExpand;

		private string _filterString;

		private FlattenerNode.Flattener _flattener;

		private bool doNotScrollOnExpanding;

		private bool _updatesLocked;

		private TreeViewControlItem _previewNodeView;

		public MultiselectionTreeViewItem RootItem
		{
			get
			{
				return (MultiselectionTreeViewItem)GetValue(RootItemProperty);
			}
			set
			{
				SetValue(RootItemProperty, value);
			}
		}

		public new IEnumerable ItemsSource
		{
			get
			{
				return base.ItemsSource;
			}
			set
			{
				throw new NotSupportedException("Use RootItem property instead");
			}
		}

		public bool RememberExpandedItems { get; set; }

		public bool AllowDragDrop { get; set; }

		public string FilterString
		{
			get
			{
				return _filterString;
			}
			set
			{
				if (_filterString != value)
				{
					bool num = RememberExpandedItems && string.IsNullOrEmpty(_filterString) && !string.IsNullOrEmpty(value);
					bool flag = RememberExpandedItems && !string.IsNullOrEmpty(_filterString) && string.IsNullOrEmpty(value);
					_filterString = value;
					if (num)
					{
						_itemsToExpand = this.GetExpandedItems();
					}
					Refilter();
					if (flag && _itemsToExpand != null)
					{
						RootItem.CollapseAllChildren();
						this.SetExpandedItems(_itemsToExpand);
						_itemsToExpand = null;
					}
					else
					{
						RootItem.ExpandAllChildren();
					}
				}
			}
		}

		public MultiselectionTreeViewItem LastClickedItem { get; private set; }

		static MultiselectionTreeView()
		{
			RootItemProperty = DependencyProperty.Register("RootItem", typeof(MultiselectionTreeViewItem), typeof(MultiselectionTreeView));
			VirtualizingStackPanel.VirtualizationModeProperty.OverrideMetadata(typeof(MultiselectionTreeView), new FrameworkPropertyMetadata(VirtualizationMode.Recycling));
		}

		public void Refilter()
		{
			MultiselectionTreeViewItem rootItem = RootItem;
			if (rootItem == null)
			{
				return;
			}
			foreach (MultiselectionTreeViewItem child in rootItem.Children)
			{
				rootItem.ApplyFilterToChild(child, _filterString);
			}
		}

		public void Expand(MultiselectionTreeViewItem node, bool expandChildren)
		{
			node.IsExpanded = true;
			if (!expandChildren)
			{
				return;
			}
			foreach (MultiselectionTreeViewItem child in node.Children)
			{
				Expand(child, expandChildren: true);
			}
		}

		public void SelectAndFocus(MultiselectionTreeViewItem node)
		{
			base.SelectedItems.Add(node);
			if (base.IsFocused)
			{
				FocusNode(node);
			}
		}

		protected override DependencyObject GetContainerForItemOverride()
		{
			return new TreeViewControlItem();
		}

		protected override bool IsItemItsOwnContainerOverride(object item)
		{
			return item is TreeViewControlItem;
		}

		protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
		{
			base.PrepareContainerForItemOverride(element, item);
			(element as TreeViewControlItem).ParentTreeView = this;
		}

		protected override void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			foreach (MultiselectionTreeViewItem removedItem in e.RemovedItems)
			{
				removedItem.IsSelected = false;
			}
			foreach (MultiselectionTreeViewItem addedItem in e.AddedItems)
			{
				addedItem.IsSelected = true;
			}
			base.OnSelectionChanged(e);
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			TreeViewControlItem treeViewControlItem = e.OriginalSource as TreeViewControlItem;
			switch (e.Key)
			{
			case Key.Left:
				if (treeViewControlItem != null && ItemsControl.ItemsControlFromItemContainer(treeViewControlItem) == this)
				{
					if (treeViewControlItem.Node.IsExpanded)
					{
						treeViewControlItem.Node.IsExpanded = false;
					}
					else if (treeViewControlItem.Node.ParentItem != null)
					{
						FocusNode(treeViewControlItem.Node.ParentItem);
					}
					e.Handled = true;
				}
				break;
			case Key.Right:
				if (treeViewControlItem != null && ItemsControl.ItemsControlFromItemContainer(treeViewControlItem) == this)
				{
					if (!treeViewControlItem.Node.IsExpanded && treeViewControlItem.Node.ShowExpander)
					{
						treeViewControlItem.Node.IsExpanded = true;
					}
					else if (treeViewControlItem.Node.Children.Count > 0)
					{
						treeViewControlItem.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
					}
					e.Handled = true;
				}
				break;
			}
			if (!e.Handled)
			{
				base.OnKeyDown(e);
			}
		}

		protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseRightButtonDown(e);
			Point position = e.GetPosition(this);
			LastClickedItem = this.GetObjectAtPoint<TreeViewControlItem>(position) as MultiselectionTreeViewItem;
		}

		protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
		{
			Point position = e.GetPosition(this);
			LastClickedItem = this.GetObjectAtPoint<TreeViewControlItem>(position) as MultiselectionTreeViewItem;
			base.OnMouseDoubleClick(e);
			LastClickedItem = null;
		}

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == RootItemProperty)
			{
				Reload();
			}
		}

		private void Reload()
		{
			if (_flattener != null)
			{
				_flattener.Unmount();
			}
			if (RootItem != null)
			{
				RootItem.IsExpanded = true;
				_flattener = new FlattenerNode.Flattener(RootItem);
				_flattener.CollectionChanged += _flattener_CollectionChanged;
				base.ItemsSource = _flattener;
			}
		}

		public void FocusNode(MultiselectionTreeViewItem node)
		{
			if (node == null)
			{
				throw new ArgumentNullException("node");
			}
			ScrollIntoView(node);
			if (base.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
			{
				OnFocusItem(node);
			}
			else
			{
				base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new DispatcherOperationCallback(OnFocusItem), node);
			}
		}

		public void HandleExpanding(MultiselectionTreeViewItem node)
		{
			if (doNotScrollOnExpanding)
			{
				return;
			}
			MultiselectionTreeViewItem multiselectionTreeViewItem = node;
			while (true)
			{
				MultiselectionTreeViewItem multiselectionTreeViewItem2 = multiselectionTreeViewItem.Children.LastOrDefault((MultiselectionTreeViewItem c) => c.IsVisible);
				if (multiselectionTreeViewItem2 == null)
				{
					break;
				}
				multiselectionTreeViewItem = multiselectionTreeViewItem2;
			}
			if (multiselectionTreeViewItem != node)
			{
				ScrollIntoView((object)multiselectionTreeViewItem);
				base.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
				{
					ScrollIntoView((object)node);
				});
			}
		}

		public void ScrollIntoView(MultiselectionTreeViewItem node)
		{
			if (node == null)
			{
				throw new ArgumentNullException("node");
			}
			doNotScrollOnExpanding = true;
			foreach (MultiselectionTreeViewItem item in node.Ancestors())
			{
				item.IsExpanded = true;
			}
			doNotScrollOnExpanding = false;
			ScrollIntoView((object)node);
		}

		public IDisposable LockUpdates()
		{
			return new UpdateLock(this);
		}

		private object OnFocusItem(object item)
		{
			if (base.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement frameworkElement)
			{
				frameworkElement.Focus();
			}
			return null;
		}

		private void _flattener_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action != NotifyCollectionChangedAction.Remove || base.Items.Count <= 0)
			{
				return;
			}
			List<MultiselectionTreeViewItem> list = null;
			foreach (MultiselectionTreeViewItem oldItem in e.OldItems)
			{
				if (oldItem.IsSelected)
				{
					if (list == null)
					{
						list = new List<MultiselectionTreeViewItem>();
					}
					list.Add(oldItem);
				}
			}
			if (!_updatesLocked && list != null)
			{
				List<MultiselectionTreeViewItem> newSelection = base.SelectedItems.Cast<MultiselectionTreeViewItem>().Except(list).ToList();
				UpdateFocusedNode(newSelection, Math.Max(0, e.OldStartingIndex - 1));
			}
		}

		private void UpdateFocusedNode(List<MultiselectionTreeViewItem> newSelection, int topSelectedIndex)
		{
			if (!_updatesLocked)
			{
				SetSelectedItems(newSelection ?? Enumerable.Empty<MultiselectionTreeViewItem>());
				if (base.SelectedItem == null)
				{
					base.SelectedIndex = topSelectedIndex;
				}
			}
		}

		public IEnumerable<MultiselectionTreeViewItem> GetTopLevelSelection()
		{
			IEnumerable<MultiselectionTreeViewItem> enumerable = base.SelectedItems.OfType<MultiselectionTreeViewItem>();
			HashSet<MultiselectionTreeViewItem> selectionHash = new HashSet<MultiselectionTreeViewItem>(enumerable);
			return enumerable.Where((MultiselectionTreeViewItem item) => item.Ancestors().All((MultiselectionTreeViewItem a) => !selectionHash.Contains(a)));
		}

		protected override void OnDragEnter(DragEventArgs e)
		{
			OnDragOver(e);
		}

		protected override void OnDragOver(DragEventArgs e)
		{
			e.Effects = DragDropEffects.None;
			if (RootItem != null)
			{
				e.Handled = true;
				e.Effects = RootItem.GetDropEffect(e, RootItem.Children.Count);
			}
		}

		protected override void OnDrop(DragEventArgs e)
		{
			e.Effects = DragDropEffects.None;
			if (RootItem != null)
			{
				e.Handled = true;
				e.Effects = RootItem.GetDropEffect(e, RootItem.Children.Count);
				if (e.Effects != 0)
				{
					RootItem.InternalDrop(e, RootItem.Children.Count);
				}
			}
		}

		internal void HandleDragEnter(TreeViewControlItem item, DragEventArgs e)
		{
			HandleDragOver(item, e);
		}

		internal void HandleDragOver(TreeViewControlItem item, DragEventArgs e)
		{
			HidePreview();
			e.Effects = DragDropEffects.None;
			DropTarget dropTarget = GetDropTarget(item, e);
			if (dropTarget != null)
			{
				e.Handled = true;
				e.Effects = dropTarget.Effect;
				ShowPreview(dropTarget.Item);
			}
		}

		internal void HandleDrop(TreeViewControlItem item, DragEventArgs e)
		{
			try
			{
				HidePreview();
				DropTarget dropTarget = GetDropTarget(item, e);
				if (dropTarget != null)
				{
					e.Handled = true;
					e.Effects = dropTarget.Effect;
					dropTarget.Node.InternalDrop(e, dropTarget.Index);
				}
			}
			catch (Exception ex)
			{
				Log.Debug(ex.ToString());
				throw;
			}
		}

		internal void HandleDragLeave(TreeViewControlItem item, DragEventArgs e)
		{
			HidePreview();
			e.Handled = true;
		}

		private DropTarget GetDropTarget(TreeViewControlItem item, DragEventArgs e)
		{
			List<DropTarget> list = BuildDropTargets(item, e);
			double y = e.GetPosition(item).Y;
			foreach (DropTarget item2 in list)
			{
				if (item2.Y >= y)
				{
					return item2;
				}
			}
			return null;
		}

		private List<DropTarget> BuildDropTargets(TreeViewControlItem item, DragEventArgs e)
		{
			List<DropTarget> list = new List<DropTarget>();
			_ = item.Node;
			TryAddDropTarget(list, item, e);
			double actualHeight = item.ActualHeight;
			double num = 0.2 * actualHeight;
			double y = actualHeight / 2.0;
			double y2 = actualHeight - num;
			if (list.Count == 2)
			{
				list[0].Y = y;
			}
			else if (list.Count == 3)
			{
				list[0].Y = num;
				list[1].Y = y2;
			}
			if (list.Count > 0)
			{
				list[list.Count - 1].Y = actualHeight;
			}
			return list;
		}

		private void TryAddDropTarget(List<DropTarget> targets, TreeViewControlItem item, DragEventArgs e)
		{
			GetNodeAndIndex(item, out var node, out var index);
			if (node != null)
			{
				DragDropEffects dropEffect = node.GetDropEffect(e, index);
				if (dropEffect != 0)
				{
					DropTarget item2 = new DropTarget
					{
						Item = item,
						Node = node,
						Index = index,
						Effect = dropEffect
					};
					targets.Add(item2);
				}
			}
		}

		private void GetNodeAndIndex(TreeViewControlItem item, out MultiselectionTreeViewItem node, out int index)
		{
			node = null;
			index = 0;
			node = item.Node;
			index = node.Children.Count;
		}

		private void ShowPreview(TreeViewControlItem item)
		{
			_previewNodeView = item;
			_previewNodeView.Background = Application.Current.TryFindResource("TreeViewItem.SelectedInactive.Background") as Brush;
		}

		private void HidePreview()
		{
			if (_previewNodeView != null)
			{
				_previewNodeView.ClearValue(Control.BackgroundProperty);
				_previewNodeView = null;
			}
		}
	}
}
