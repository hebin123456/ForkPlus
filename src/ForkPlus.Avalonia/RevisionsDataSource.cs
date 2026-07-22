using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using ForkPlus.Git;

// Avalonia spike 版 RevisionsDataSource（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/RevisionsDataSource.cs（864 行）：
//   - WPF: public class RevisionsDataSource : IList, ICollection, IEnumerable, INotifyCollectionChanged
//   - 内嵌 struct Line（Sha NextSha + byte Id）
//   - 内嵌 struct Page（分页：PageSize=100，Range Rows，NextPage，RevisionHeadersPreloadPossible）
//   - Action OnFetchRevisionsNeeded
//   - JobQueue _jobQueue / RepositoryStashes / RepositoryReferences / RepositoryRemotes / RepositoryWorktrees
//   - bool _showStashesInRevisionList / _showTags / _collapseMerges
//   - List<DecoratedRevision> _decoratedRevisions
//   - int Count / DecoratedRevision this[int index] / SyncRoot / IsSynchronized / IsFixedSize / IsReadOnly
//   - Add / Clear / Contains / IndexOf / Insert / Remove / RemoveAt / CopyTo
//   - GetEnumerator / OnFetchRevisionsNeeded
//   - UpdateData / LoadPage / FetchRevisions / EnsureRevisionHeadersLoaded
//   - 依赖：JobQueue / RepositoryStashes / RepositoryReferences / RepositoryRemotes / RepositoryWorktrees /
//     RevisionVisualGraph / DecoratedRevision / GitModule / System.Windows.Media
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF System.Windows.IList → System.Collections.IList（跨平台，零改动）
//   2. WPF INotifyCollectionChanged → System.Collections.Specialized（跨平台，零改动）
//   3. spike 跳过 JobQueue / GitModule / RevisionVisualGraph 复杂依赖
//   4. spike 跳过 Page 分页逻辑 + LoadPage / FetchRevisions 异步加载
//   5. spike 跳过 RepositoryStashes / RepositoryReferences / RepositoryRemotes / RepositoryWorktrees
//   6. spike 仅保留 IList + INotifyCollectionChanged 接口实现（空数据源占位）
//
// spike 简化（task spec 关键 API）：
//   - IList + INotifyCollectionChanged 接口实现（空集合占位）
//   - Count / this[index] / Add / Clear / Contains / IndexOf / Insert / Remove / RemoveAt / CopyTo / GetEnumerator
namespace ForkPlus.Avalonia
{
    public class RevisionsDataSource : IList, ICollection, IEnumerable, INotifyCollectionChanged
    {
        private readonly List<DecoratedRevision> _decoratedRevisions = new List<DecoratedRevision>();

        // spike 版：跳过 Page 分页 + JobQueue + GitModule 依赖
        public Action OnFetchRevisionsNeeded;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public int Count => _decoratedRevisions.Count;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;

        public object this[int index]
        {
            get => _decoratedRevisions[index];
            set => _decoratedRevisions[index] = (DecoratedRevision)value;
        }

        public RevisionsDataSource()
        {
            // spike 版：空构造（无 JobQueue / GitModule 注入）
        }

        public int Add(object value)
        {
            int index = _decoratedRevisions.Count;
            _decoratedRevisions.Add((DecoratedRevision)value);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
            return index;
        }

        public void Clear()
        {
            _decoratedRevisions.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(object value)
        {
            return _decoratedRevisions.Contains((DecoratedRevision)value);
        }

        public int IndexOf(object value)
        {
            return _decoratedRevisions.IndexOf((DecoratedRevision)value);
        }

        public void Insert(int index, object value)
        {
            _decoratedRevisions.Insert(index, (DecoratedRevision)value);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
        }

        public void Remove(object value)
        {
            int index = _decoratedRevisions.IndexOf((DecoratedRevision)value);
            if (index >= 0)
            {
                _decoratedRevisions.RemoveAt(index);
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, value, index));
            }
        }

        public void RemoveAt(int index)
        {
            object value = _decoratedRevisions[index];
            _decoratedRevisions.RemoveAt(index);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, value, index));
        }

        public void CopyTo(Array array, int index)
        {
            ((ICollection)_decoratedRevisions).CopyTo(array, index);
        }

        public IEnumerator GetEnumerator()
        {
            return _decoratedRevisions.GetEnumerator();
        }
    }
}
