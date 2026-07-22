using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ForkPlus.Avalonia.Controls.Flattener
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Flattener/FlattenerNode.cs（519 行）：
    //   - public abstract class FlattenerNode
    //   - 嵌套类 Flattener : IList, ICollection, IEnumerable, INotifyCollectionChanged
    //     （AVL 树扁平化列表，维护可视节点的有序索引）
    //   - AVL 树操作：TotalCount / Rebalance / LLRotate / RRRotate / InsertNodeAfter /
    //     RemoveNodes / DeleteNode / Successor / ReplaceWith / ConcatTrees
    //   - GetListRoot：向上遍历到根节点，WPF 返回 MultiselectionTreeViewItem
    //   - IsVisible 属性 + InvalidateParents（失效计数缓存）
    //   - GetNodeByVisibleIndex / GetVisibleIndexForNode：按可视索引查找节点
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF MultiselectionTreeViewItem 继承 FlattenerNode → Avalonia spike 版
    //      MultiselectionTreeViewItem 不继承 FlattenerNode（spike 简化为 INotifyPropertyChanged）
    //   2. GetListRoot() 返回类型：WPF MultiselectionTreeViewItem → Avalonia FlattenerNode
    //      （spike 版自包含，不依赖 MultiselectionTreeViewItem）
    //   3. Flattener.IndexOf：WPF 检查 item is MultiselectionTreeViewItem →
    //      Avalonia 检查 item is FlattenerNode
    //   4. NodesInserted/Removed：WPF 参数 IEnumerable<MultiselectionTreeViewItem> →
    //      Avalonia IEnumerable<FlattenerNode>
    //   5. AVL 树核心逻辑（TotalCount / Rebalance / Rotate / Insert / Delete）保持不变（纯 C#）
    //   6. namespace 改为 ForkPlus.Avalonia.Controls.Flattener
    //
    // spike 简化：
    //   - 保留完整 AVL 树逻辑（无 UI 依赖，零改动）
    //   - GetListRoot 返回 FlattenerNode（不再 cast 为 MultiselectionTreeViewItem）
    //   - Flattener.IndexOf / NodesInserted / NodesRemoved 用 FlattenerNode 替代 MultiselectionTreeViewItem
    public abstract class FlattenerNode
    {
        public class Flattener : IList, ICollection, IEnumerable, INotifyCollectionChanged
        {
            public FlattenerNode _root;

            private readonly object _syncRoot = new object();

            public object this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                    int index2 = index + 1;
                    return GetNodeByVisibleIndex(_root, index2);
                }
                set
                {
                    throw new NotSupportedException();
                }
            }

            public bool IsReadOnly => true;

            public bool IsFixedSize => false;

            public int Count => _root.TotalCount() - 1;

            public object SyncRoot => _syncRoot;

            public bool IsSynchronized => false;

            public event NotifyCollectionChangedEventHandler CollectionChanged;

            public Flattener(FlattenerNode root)
            {
                root = root.GetListRoot();
                _root = root;
                root._treeFlattener = this;
            }

            public void Unmount()
            {
                _root._treeFlattener = null;
            }

            public int Add(object value)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(object item)
            {
                return IndexOf(item) >= 0;
            }

            public void CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }

            public IEnumerator GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                {
                    yield return this[i];
                }
            }

            // 对照 WPF: if (item is MultiselectionTreeViewItem { IsVisible: not false } ... &&
            //           multiselectionTreeViewItem.GetListRoot() == _root)
            // Avalonia spike: item is FlattenerNode（不再依赖 MultiselectionTreeViewItem）
            public int IndexOf(object item)
            {
                if (item is FlattenerNode { IsVisible: not false } node && node.GetListRoot() == _root)
                {
                    return GetVisibleIndexForNode(node) - 1;
                }
                return -1;
            }

            public void Insert(int index, object value)
            {
                throw new NotImplementedException();
            }

            public void Remove(object value)
            {
                throw new NotImplementedException();
            }

            public void RemoveAt(int index)
            {
                throw new NotImplementedException();
            }

            // 对照 WPF: public void NodesInserted(int index, IEnumerable<MultiselectionTreeViewItem> nodes)
            // Avalonia spike: IEnumerable<FlattenerNode>
            public void NodesInserted(int index, IEnumerable<FlattenerNode> nodes)
            {
                index--;
                foreach (FlattenerNode node in nodes)
                {
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, node, index++));
                }
            }

            // 对照 WPF: public void NodesRemoved(int index, IEnumerable<MultiselectionTreeViewItem> nodes)
            // Avalonia spike: IEnumerable<FlattenerNode>
            public void NodesRemoved(int index, IEnumerable<FlattenerNode> nodes)
            {
                index--;
                foreach (FlattenerNode node in nodes)
                {
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, node, index));
                }
            }
        }

        protected Flattener _treeFlattener;

        private FlattenerNode _Parent;
        private FlattenerNode _left;
        private FlattenerNode _right;
        private int _totalCount = -1;
        private byte _height = 1;

        public bool IsVisible { get; protected set; } = true;

        private int Balance => Height(_right) - Height(_left);

        // 对照 WPF: public MultiselectionTreeViewItem GetListRoot()
        // Avalonia spike: 返回 FlattenerNode（不再 cast 为 MultiselectionTreeViewItem）
        public FlattenerNode GetListRoot()
        {
            FlattenerNode flattenerNode = this;
            while (flattenerNode._Parent != null)
            {
                flattenerNode = flattenerNode._Parent;
            }
            return flattenerNode;
        }

        private int TotalCount()
        {
            if (_totalCount > 0)
            {
                return _totalCount;
            }
            int num = (IsVisible ? 1 : 0);
            if (_left != null)
            {
                num += _left.TotalCount();
            }
            if (_right != null)
            {
                num += _right.TotalCount();
            }
            _totalCount = num;
            return _totalCount;
        }

        protected void InvalidateParents()
        {
            FlattenerNode flattenerNode = this;
            while (flattenerNode != null && flattenerNode._totalCount >= 0)
            {
                flattenerNode._totalCount = -1;
                flattenerNode = flattenerNode._Parent;
            }
        }

        protected virtual void OnIsVisibleChanged()
        {
        }

        private static int Height(FlattenerNode node)
        {
            return node?._height ?? 0;
        }

        protected void CheckRootInvariants()
        {
            GetListRoot().CheckInvariants();
        }

        private void CheckInvariants()
        {
            if (_left != null)
            {
                _left.CheckInvariants();
            }
            if (_right != null)
            {
                _right.CheckInvariants();
            }
        }

        internal static FlattenerNode GetNodeByVisibleIndex(FlattenerNode root, int index)
        {
            root.TotalCount();
            FlattenerNode flattenerNode = root;
            while (true)
            {
                if (flattenerNode._left != null && index < flattenerNode._left._totalCount)
                {
                    flattenerNode = flattenerNode._left;
                    continue;
                }
                if (flattenerNode._left != null)
                {
                    index -= flattenerNode._left._totalCount;
                }
                if (flattenerNode.IsVisible)
                {
                    if (index == 0)
                    {
                        break;
                    }
                    index--;
                }
                flattenerNode = flattenerNode._right;
            }
            return flattenerNode;
        }

        internal static int GetVisibleIndexForNode(FlattenerNode node)
        {
            int num = ((node._left != null) ? node._left.TotalCount() : 0);
            while (node._Parent != null)
            {
                if (node == node._Parent._right)
                {
                    if (node._Parent._left != null)
                    {
                        num += node._Parent._left.TotalCount();
                    }
                    if (node._Parent.IsVisible)
                    {
                        num++;
                    }
                }
                node = node._Parent;
            }
            return num;
        }

        private static FlattenerNode Rebalance(FlattenerNode node)
        {
            while (Math.Abs(node.Balance) > 1)
            {
                if (node.Balance > 1)
                {
                    if (node._right.Balance < 0)
                    {
                        node._right = node._right.RRRotate();
                    }
                    node = node.LLRotate();
                    node._left = Rebalance(node._left);
                }
                else if (node.Balance < -1)
                {
                    if (node._left.Balance > 0)
                    {
                        node._left = node._left.LLRotate();
                    }
                    node = node.RRRotate();
                    node._right = Rebalance(node._right);
                }
            }
            node._height = (byte)(1 + Math.Max(Height(node._left), Height(node._right)));
            node._totalCount = -1;
            return node;
        }

        private FlattenerNode LLRotate()
        {
            FlattenerNode left = _right._left;
            FlattenerNode right = _right;
            if (left != null)
            {
                left._Parent = this;
            }
            _right = left;
            right._left = this;
            right._Parent = _Parent;
            _Parent = right;
            right._left = Rebalance(this);
            return right;
        }

        private FlattenerNode RRRotate()
        {
            FlattenerNode right = _left._right;
            FlattenerNode left = _left;
            if (right != null)
            {
                right._Parent = this;
            }
            _left = right;
            left._right = this;
            left._Parent = _Parent;
            _Parent = left;
            left._right = Rebalance(this);
            return left;
        }

        private static void RebalanceUntilRoot(FlattenerNode pos)
        {
            while (pos._Parent != null)
            {
                pos = ((pos != pos._Parent._left) ? (pos._Parent._right = Rebalance(pos)) : (pos._Parent._left = Rebalance(pos)));
                pos = pos._Parent;
            }
            FlattenerNode flattenerNode = Rebalance(pos);
            if (flattenerNode != pos && pos._treeFlattener != null)
            {
                flattenerNode._treeFlattener = pos._treeFlattener;
                pos._treeFlattener = null;
                flattenerNode._treeFlattener._root = flattenerNode;
            }
        }

        protected static void InsertNodeAfter(FlattenerNode pos, FlattenerNode newNode)
        {
            newNode = newNode.GetListRoot();
            if (pos._right == null)
            {
                pos._right = newNode;
                newNode._Parent = pos;
            }
            else
            {
                pos = pos._right;
                while (pos._left != null)
                {
                    pos = pos._left;
                }
                pos._left = newNode;
                newNode._Parent = pos;
            }
            RebalanceUntilRoot(pos);
        }

        protected void RemoveNodes(FlattenerNode start, FlattenerNode end)
        {
            List<FlattenerNode> list = new List<FlattenerNode>();
            FlattenerNode flattenerNode = start;
            FlattenerNode flattenerNode4;
            do
            {
                HashSet<FlattenerNode> hashSet = new HashSet<FlattenerNode>();
                for (FlattenerNode flattenerNode2 = end; flattenerNode2 != null; flattenerNode2 = flattenerNode2._Parent)
                {
                    hashSet.Add(flattenerNode2);
                }
                list.Add(flattenerNode);
                if (!hashSet.Contains(flattenerNode) && flattenerNode._right != null)
                {
                    list.Add(flattenerNode._right);
                    flattenerNode._right._Parent = null;
                    flattenerNode._right = null;
                }
                FlattenerNode flattenerNode3 = flattenerNode.Successor();
                DeleteNode(flattenerNode);
                flattenerNode4 = flattenerNode;
                flattenerNode = flattenerNode3;
            }
            while (flattenerNode4 != end);
            FlattenerNode first = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                first = ConcatTrees(first, list[i]);
            }
        }

        private static FlattenerNode ConcatTrees(FlattenerNode first, FlattenerNode second)
        {
            FlattenerNode flattenerNode = first;
            while (flattenerNode._right != null)
            {
                flattenerNode = flattenerNode._right;
            }
            InsertNodeAfter(flattenerNode, second);
            return flattenerNode.GetListRoot();
        }

        private FlattenerNode Successor()
        {
            if (_right != null)
            {
                FlattenerNode flattenerNode = _right;
                while (flattenerNode._left != null)
                {
                    flattenerNode = flattenerNode._left;
                }
                return flattenerNode;
            }
            FlattenerNode flattenerNode2 = this;
            FlattenerNode flattenerNode3;
            do
            {
                flattenerNode3 = flattenerNode2;
                flattenerNode2 = flattenerNode2._Parent;
            }
            while (flattenerNode2 != null && flattenerNode2._right == flattenerNode3);
            return flattenerNode2;
        }

        private static void DeleteNode(FlattenerNode node)
        {
            FlattenerNode flattenerNode;
            if (node._left == null)
            {
                flattenerNode = node._Parent;
                node.ReplaceWith(node._right);
                node._right = null;
            }
            else if (node._right == null)
            {
                flattenerNode = node._Parent;
                node.ReplaceWith(node._left);
                node._left = null;
            }
            else
            {
                FlattenerNode flattenerNode2 = node._right;
                while (flattenerNode2._left != null)
                {
                    flattenerNode2 = flattenerNode2._left;
                }
                flattenerNode = flattenerNode2._Parent;
                flattenerNode2.ReplaceWith(flattenerNode2._right);
                flattenerNode2._right = null;
                flattenerNode2._left = node._left;
                node._left = null;
                flattenerNode2._right = node._right;
                node._right = null;
                if (flattenerNode2._left != null)
                {
                    flattenerNode2._left._Parent = flattenerNode2;
                }
                if (flattenerNode2._right != null)
                {
                    flattenerNode2._right._Parent = flattenerNode2;
                }
                node.ReplaceWith(flattenerNode2);
                if (flattenerNode == node)
                {
                    flattenerNode = flattenerNode2;
                }
            }
            node._height = 1;
            node._totalCount = -1;
            if (flattenerNode != null)
            {
                RebalanceUntilRoot(flattenerNode);
            }
        }

        private void ReplaceWith(FlattenerNode node)
        {
            if (_Parent != null)
            {
                if (_Parent._left == this)
                {
                    _Parent._left = node;
                }
                else
                {
                    _Parent._right = node;
                }
                if (node != null)
                {
                    node._Parent = _Parent;
                }
                _Parent = null;
            }
            else
            {
                node._Parent = null;
                if (_treeFlattener != null)
                {
                    node._treeFlattener = _treeFlattener;
                    _treeFlattener = null;
                    node._treeFlattener._root = node;
                }
            }
        }
    }
}
