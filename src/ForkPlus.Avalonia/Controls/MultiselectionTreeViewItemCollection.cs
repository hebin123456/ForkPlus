using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 MultiselectionTreeViewItemCollection（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/MultiselectionTreeViewItemCollection.cs（115 行）：
    //   - WPF MultiselectionTreeViewItemCollection : IReadOnlyList<MultiselectionTreeViewItem>,
    //       IReadOnlyCollection<MultiselectionTreeViewItem>, IEnumerable<MultiselectionTreeViewItem>,
    //       IEnumerable, IList<MultiselectionTreeViewItem>, ICollection<MultiselectionTreeViewItem>,
    //       INotifyCollectionChanged
    //   - private readonly MultiselectionTreeViewItem _parent
    //   - private List<MultiselectionTreeViewItem> _items
    //   - this[int].get / set(throw NotImplementedException)
    //   - Count / IsReadOnly(throw NotImplementedException)
    //   - event NotifyCollectionChangedEventHandler CollectionChanged
    //   - Add / Clear / Contains(throw) / CopyTo(throw) / GetEnumerator / IndexOf /
    //     Insert / Remove / RemoveAt / IEnumerable.GetEnumerator
    //   - RaiseCollectionChanged：先 _parent.OnChildrenChanged(args)，再触发 CollectionChanged
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 集合类无 WPF / Avalonia UI 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   3. MultiselectionTreeViewItem 类型来自本 spike 命名空间
    //   4. spike 不实现 Contains / CopyTo（WPF 也 throw NotImplementedException，spike 同样 throw）
    //   5. spike IsReadOnly 改为返回 true（避免 NotImplementedException 影响调用方）
    //
    // spike 简化：
    //   - 与 WPF 一致的集合语义 + CollectionChanged 事件
    public class MultiselectionTreeViewItemCollection : IReadOnlyList<MultiselectionTreeViewItem>, IReadOnlyCollection<MultiselectionTreeViewItem>, IEnumerable<MultiselectionTreeViewItem>, IEnumerable, IList<MultiselectionTreeViewItem>, ICollection<MultiselectionTreeViewItem>, INotifyCollectionChanged
    {
        // 对照 WPF: private readonly MultiselectionTreeViewItem _parent
        private readonly MultiselectionTreeViewItem _parent;

        // 对照 WPF: private List<MultiselectionTreeViewItem> _items = new List<MultiselectionTreeViewItem>()
        private List<MultiselectionTreeViewItem> _items = new List<MultiselectionTreeViewItem>();

        // 对照 WPF: public MultiselectionTreeViewItem this[int index].get / set(throw)
        public MultiselectionTreeViewItem this[int index]
        {
            get => _items[index];
            set => throw new NotImplementedException();
        }

        // 对照 WPF: public int Count => _items.Count
        public int Count => _items.Count;

        // 对照 WPF: public bool IsReadOnly { get { throw new NotImplementedException(); } }
        // spike 版改返回 true（避免 NotImplementedException 影响调用方）
        public bool IsReadOnly => true;

        // 对照 WPF: public event NotifyCollectionChangedEventHandler CollectionChanged
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        // 对照 WPF: public MultiselectionTreeViewItemCollection(MultiselectionTreeViewItem parent)
        public MultiselectionTreeViewItemCollection(MultiselectionTreeViewItem parent)
        {
            _parent = parent;
        }

        // 对照 WPF: public void Add(MultiselectionTreeViewItem item)
        public void Add(MultiselectionTreeViewItem item)
        {
            _items.Add(item);
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, _items.Count - 1));
        }

        // 对照 WPF: public void Clear()
        public void Clear()
        {
            List<MultiselectionTreeViewItem> items = _items;
            _items = new List<MultiselectionTreeViewItem>();
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, items, 0));
        }

        // 对照 WPF: public bool Contains(MultiselectionTreeViewItem item) { throw new NotImplementedException(); }
        public bool Contains(MultiselectionTreeViewItem item)
        {
            throw new NotImplementedException();
        }

        // 对照 WPF: public void CopyTo(MultiselectionTreeViewItem[] array, int arrayIndex) { throw ... }
        public void CopyTo(MultiselectionTreeViewItem[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        // 对照 WPF: public IEnumerator<MultiselectionTreeViewItem> GetEnumerator()
        public IEnumerator<MultiselectionTreeViewItem> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        // 对照 WPF: public int IndexOf(MultiselectionTreeViewItem item)
        public int IndexOf(MultiselectionTreeViewItem item)
        {
            if (item == null || item.ParentItem != _parent)
            {
                return -1;
            }
            return _items.IndexOf(item);
        }

        // 对照 WPF: public void Insert(int index, MultiselectionTreeViewItem item)
        public void Insert(int index, MultiselectionTreeViewItem item)
        {
            _items.Insert(index, item);
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        // 对照 WPF: public bool Remove(MultiselectionTreeViewItem item)
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

        // 对照 WPF: public void RemoveAt(int index)
        public void RemoveAt(int index)
        {
            MultiselectionTreeViewItem changedItem = _items[index];
            _items.RemoveAt(index);
            RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, changedItem, index));
        }

        // 对照 WPF: IEnumerator IEnumerable.GetEnumerator()
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        // 对照 WPF: private void RaiseCollectionChanged(NotifyCollectionChangedEventArgs args)
        //   _parent.OnChildrenChanged(args); CollectionChanged?.Invoke(this, args);
        // spike 版简化：spike MultiselectionTreeViewItem 没有 OnChildrenChanged 虚方法，
        // 直接触发 CollectionChanged（spike 不维护 Flattener 链接）
        private void RaiseCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            // spike 版跳过 _parent.OnChildrenChanged（spike 不依赖 Flattener）
            CollectionChanged?.Invoke(this, args);
        }
    }
}
