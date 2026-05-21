using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ForkPlus.UI.Controls
{
	public class MultiselectionTreeViewItemCollection : IReadOnlyList<MultiselectionTreeViewItem>, IReadOnlyCollection<MultiselectionTreeViewItem>, IEnumerable<MultiselectionTreeViewItem>, IEnumerable, IList<MultiselectionTreeViewItem>, ICollection<MultiselectionTreeViewItem>, INotifyCollectionChanged
	{
		private readonly MultiselectionTreeViewItem _parent;

		private List<MultiselectionTreeViewItem> _items = new List<MultiselectionTreeViewItem>();

		public MultiselectionTreeViewItem this[int index]
		{
			get
			{
				return _items[index];
			}
			set
			{
				throw new NotImplementedException();
			}
		}

		public int Count => _items.Count;

		public bool IsReadOnly
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public MultiselectionTreeViewItemCollection(MultiselectionTreeViewItem parent)
		{
			_parent = parent;
		}

		public void Add(MultiselectionTreeViewItem item)
		{
			_items.Add(item);
			RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, _items.Count - 1));
		}

		public void Clear()
		{
			List<MultiselectionTreeViewItem> items = _items;
			_items = new List<MultiselectionTreeViewItem>();
			RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, items, 0));
		}

		public bool Contains(MultiselectionTreeViewItem item)
		{
			throw new NotImplementedException();
		}

		public void CopyTo(MultiselectionTreeViewItem[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public IEnumerator<MultiselectionTreeViewItem> GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		public int IndexOf(MultiselectionTreeViewItem item)
		{
			if (item == null || item.ParentItem != _parent)
			{
				return -1;
			}
			return _items.IndexOf(item);
		}

		public void Insert(int index, MultiselectionTreeViewItem item)
		{
			_items.Insert(index, item);
			RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
		}

		public bool Remove(MultiselectionTreeViewItem item)
		{
			int num = IndexOf(item);
			if (num >= 0)
			{
				RemoveAt(num);
				return true;
			}
			return false;
		}

		public void RemoveAt(int index)
		{
			MultiselectionTreeViewItem changedItem = _items[index];
			_items.RemoveAt(index);
			RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, changedItem, index));
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		private void RaiseCollectionChanged(NotifyCollectionChangedEventArgs args)
		{
			_parent.OnChildrenChanged(args);
			this.CollectionChanged?.Invoke(this, args);
		}
	}
}
