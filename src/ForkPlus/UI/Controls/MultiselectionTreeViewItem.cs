using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using ForkPlus.UI.Controls.Flattener;

namespace ForkPlus.UI.Controls
{
	public class MultiselectionTreeViewItem : FlattenerNode, INotifyPropertyChanged
	{
		private bool _isExpanded;

		private bool _isHidden;

		private bool _isSelected;

		private ListBoxSelectionType _selectionType;

		private MultiselectionTreeViewItemCollection _children;

		public MultiselectionTreeViewItem ParentItem { get; private set; }

		public bool IsExpanded
		{
			get
			{
				return _isExpanded;
			}
			set
			{
				if (_isExpanded != value)
				{
					_isExpanded = value;
					if (_isExpanded)
					{
						OnExpanding();
					}
					else
					{
						OnCollapsing();
					}
					UpdateChildIsVisible(updateFlattener: true);
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsExpanded"));
				}
			}
		}

		public bool IsHidden
		{
			get
			{
				return _isHidden;
			}
			set
			{
				if (_isHidden != value)
				{
					_isHidden = value;
					if (ParentItem != null)
					{
						UpdateIsVisible(ParentItem.IsVisible && ParentItem.IsExpanded, updateFlattener: true);
						ParentItem?.RaisePropertyChanged("ShowExpander");
					}
				}
			}
		}

		public bool IsSelected
		{
			get
			{
				return _isSelected;
			}
			set
			{
				if (_isSelected != value)
				{
					_isSelected = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
				}
			}
		}

		public ListBoxSelectionType SelectionType
		{
			get
			{
				return _selectionType;
			}
			set
			{
				if (_selectionType != value)
				{
					_selectionType = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectionType"));
				}
			}
		}

		public bool HasChildren
		{
			get
			{
				if (_children != null)
				{
					return _children.Count > 0;
				}
				return false;
			}
		}

		public int Level
		{
			get
			{
				if (ParentItem == null)
				{
					return 0;
				}
				return ParentItem.Level + 1;
			}
		}

		public virtual bool ShowExpander => (_children?.Count ?? 0) != 0;

		public virtual bool IsFocusable => true;

		public string Title { get; set; }

		public MultiselectionTreeViewItemCollection Children
		{
			get
			{
				if (_children == null)
				{
					_children = new MultiselectionTreeViewItemCollection(this);
				}
				return _children;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void UpdateChildIsVisible(bool updateFlattener)
		{
			if (_children == null || _children.Count <= 0)
			{
				return;
			}
			bool parentIsVisibleAndExpanded = base.IsVisible && IsExpanded;
			foreach (MultiselectionTreeViewItem child in _children)
			{
				child.UpdateIsVisible(parentIsVisibleAndExpanded, updateFlattener);
			}
		}

		protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public virtual void StartDrag(DependencyObject dragSource, MultiselectionTreeViewItem[] nodes)
		{
		}

		public virtual DragDropEffects GetDropEffect(DragEventArgs e, int index)
		{
			return DragDropEffects.None;
		}

		internal void InternalDrop(DragEventArgs e, int index)
		{
			Drop(e, index);
		}

		public virtual void Drop(DragEventArgs e, int index)
		{
		}

		protected virtual IDataObject GetDataObject(MultiselectionTreeViewItem[] nodes)
		{
			return null;
		}

		public IEnumerable<MultiselectionTreeViewItem> Ancestors()
		{
			for (MultiselectionTreeViewItem node = ParentItem; node != null; node = node.ParentItem)
			{
				yield return node;
			}
		}

		public MultiselectionTreeViewItem Previous()
		{
			Flattener treeFlattener = GetListRoot()._treeFlattener;
			if (treeFlattener != null)
			{
				int num = treeFlattener.IndexOf(this) - 1;
				if (num >= 0 && num < treeFlattener.Count)
				{
					return treeFlattener[num] as MultiselectionTreeViewItem;
				}
			}
			return null;
		}

		public MultiselectionTreeViewItem Next()
		{
			Flattener treeFlattener = GetListRoot()._treeFlattener;
			if (treeFlattener != null)
			{
				int num = treeFlattener.IndexOf(this) + 1;
				if (num >= 0 && num < treeFlattener.Count)
				{
					return treeFlattener[num] as MultiselectionTreeViewItem;
				}
			}
			return null;
		}

		public int FlatIndex()
		{
			return GetListRoot()._treeFlattener?.IndexOf(this) ?? (-1);
		}

		public void ApplyFilterToChild(MultiselectionTreeViewItem child, string filterString)
		{
			bool flag = child.MatchFilter(filterString);
			if (child.HasChildren)
			{
				child.EnsureChildrenFiltered(filterString);
				child.IsHidden = child.AreAllChildrenHidden();
			}
			else
			{
				child.IsHidden = !flag;
			}
		}

		internal void EnsureChildrenFiltered(string filterString)
		{
			foreach (MultiselectionTreeViewItem child in Children)
			{
				ApplyFilterToChild(child, filterString);
			}
		}

		private bool AreAllChildrenHidden()
		{
			if (_children == null)
			{
				return true;
			}
			for (int i = 0; i < _children.Count; i++)
			{
				if (!_children[i].IsHidden)
				{
					return false;
				}
			}
			return true;
		}

		protected virtual void OnExpanding()
		{
		}

		protected virtual void OnCollapsing()
		{
		}

		protected virtual bool MatchFilter(string filterString)
		{
			if (string.IsNullOrEmpty(filterString))
			{
				return true;
			}
			if (Title.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) != -1)
			{
				return true;
			}
			return false;
		}

		protected internal virtual void OnChildrenChanged(NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
			{
				foreach (MultiselectionTreeViewItem oldItem in e.OldItems)
				{
					oldItem.ParentItem = null;
					MultiselectionTreeViewItem multiselectionTreeViewItem2 = oldItem;
					while (multiselectionTreeViewItem2._children != null && multiselectionTreeViewItem2._children.Count > 0)
					{
						multiselectionTreeViewItem2 = multiselectionTreeViewItem2._children.Last();
					}
					List<MultiselectionTreeViewItem> list = null;
					int index = 0;
					if (oldItem.IsVisible)
					{
						index = FlattenerNode.GetVisibleIndexForNode(oldItem);
						list = oldItem.VisibleDescendantsAndSelf().ToList();
					}
					RemoveNodes(oldItem, multiselectionTreeViewItem2);
					if (list != null)
					{
						GetListRoot()._treeFlattener?.NodesRemoved(index, list);
					}
				}
			}
			if (e.NewItems != null)
			{
				MultiselectionTreeViewItem multiselectionTreeViewItem3 = ((e.NewStartingIndex == 0) ? null : _children[e.NewStartingIndex - 1]);
				foreach (MultiselectionTreeViewItem newItem in e.NewItems)
				{
					newItem.ParentItem = this;
					newItem.UpdateIsVisible(base.IsVisible && IsExpanded, updateFlattener: false);
					while (multiselectionTreeViewItem3 != null && multiselectionTreeViewItem3._children?.Count > 0)
					{
						multiselectionTreeViewItem3 = multiselectionTreeViewItem3._children.Last();
					}
					FlattenerNode.InsertNodeAfter(multiselectionTreeViewItem3 ?? this, newItem);
					multiselectionTreeViewItem3 = newItem;
					if (newItem.IsVisible)
					{
						GetListRoot()._treeFlattener?.NodesInserted(FlattenerNode.GetVisibleIndexForNode(newItem), newItem.VisibleDescendantsAndSelf());
					}
				}
			}
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ShowExpander"));
		}

		internal IEnumerable<MultiselectionTreeViewItem> VisibleDescendantsAndSelf()
		{
			return TreeTraversal.PreOrder(this, (MultiselectionTreeViewItem n) => n.Children.Where((MultiselectionTreeViewItem c) => c.IsVisible));
		}

		private void UpdateIsVisible(bool parentIsVisibleAndExpanded, bool updateFlattener)
		{
			bool flag = parentIsVisibleAndExpanded && !IsHidden;
			if (base.IsVisible == flag)
			{
				return;
			}
			base.IsVisible = flag;
			InvalidateParents();
			List<MultiselectionTreeViewItem> list = null;
			if (updateFlattener && !flag)
			{
				list = VisibleDescendantsAndSelf().ToList();
			}
			UpdateChildIsVisible(updateFlattener: false);
			if (updateFlattener)
			{
				CheckRootInvariants();
			}
			if (list != null)
			{
				Flattener treeFlattener = GetListRoot()._treeFlattener;
				if (treeFlattener != null)
				{
					treeFlattener.NodesRemoved(FlattenerNode.GetVisibleIndexForNode(this), list);
					foreach (MultiselectionTreeViewItem item in list)
					{
						item.OnIsVisibleChanged();
					}
				}
			}
			if (!(updateFlattener && flag))
			{
				return;
			}
			Flattener treeFlattener2 = GetListRoot()._treeFlattener;
			if (treeFlattener2 == null)
			{
				return;
			}
			treeFlattener2.NodesInserted(FlattenerNode.GetVisibleIndexForNode(this), VisibleDescendantsAndSelf());
			foreach (MultiselectionTreeViewItem item2 in VisibleDescendantsAndSelf())
			{
				item2.OnIsVisibleChanged();
			}
		}
	}
}
