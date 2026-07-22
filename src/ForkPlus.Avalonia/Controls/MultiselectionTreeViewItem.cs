using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 MultiselectionTreeViewItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/MultiselectionTreeViewItem.cs（388 行）：
    //   - WPF MultiselectionTreeViewItem : FlattenerNode, INotifyPropertyChanged
    //     （FlattenerNode 是 WPF 自定义抽象基类，维护扁平化可视树）
    //   - IsExpanded / IsHidden / IsSelected / SelectionType 属性 + PropertyChanged 通知
    //   - HasChildren / Level / ShowExpander / IsFocusable / Title 属性
    //   - Children (MultiselectionTreeViewItemCollection)
    //   - ParentItem（私有 setter）
    //   - Ancestors() / Previous() / Next() / FlatIndex() / ApplyFilterToChild / ...
    //   - virtual StartDrag / GetDropEffect / Drop / GetDataObject
    //   - protected virtual OnExpanding / OnCollapsing / MatchFilter
    //   - internal OnChildrenChanged（由 collection 调用，维护 Flattener 链接）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 FlattenerNode → spike 直接 object + INotifyPropertyChanged
    //      （FlattenerNode + Flattener 复杂扁平化可视树未迁移到 Avalonia）
    //   2. spike 跳过 Flattener 链接维护（OnChildrenChanged 内的 NodesInserted/Removed）
    //   3. spike 跳过 Previous/Next/FlatIndex（依赖 Flattener.IndexOf）
    //   4. spike 跳过 VisibleDescendantsAndSelf / UpdateIsVisible（依赖 Flattener）
    //   5. spike 跳过 StartDrag / GetDropEffect / Drop / GetDataObject（依赖 WPF DragDrop）
    //   6. spike 保留 INotifyPropertyChanged + IsExpanded / IsSelected / SelectionType /
    //      HasChildren / Level / ShowExpander / IsFocusable / Title / Children / ParentItem /
    //      Ancestors / ApplyFilterToChild / MatchFilter / RaisePropertyChanged
    //   7. spike 保留 ListBoxSelectionType（来自 ForkPlus.Core.UI）
    //
    // spike 简化（task spec 关键 API）：
    //   - INotifyPropertyChanged 实现
    //   - IsExpanded / IsSelected / SelectionType / Title / Children / ParentItem 属性
    //   - HasChildren / Level / ShowExpander 计算属性
    //   - Ancestors() / ApplyFilterToChild / MatchFilter 方法
    //   - RaisePropertyChanged 辅助方法
    public class MultiselectionTreeViewItem : INotifyPropertyChanged
    {
        // 对照 WPF: private bool _isExpanded
        private bool _isExpanded;

        // 对照 WPF: private bool _isHidden
        private bool _isHidden;

        // 对照 WPF: private bool _isSelected
        private bool _isSelected;

        // 对照 WPF: private ListBoxSelectionType _selectionType
        private ListBoxSelectionType _selectionType;

        // 对照 WPF: private MultiselectionTreeViewItemCollection _children
        private MultiselectionTreeViewItemCollection _children;

        // 对照 WPF: public MultiselectionTreeViewItem ParentItem { get; private set; }
        public MultiselectionTreeViewItem ParentItem { get; private set; }

        // 对照 WPF: public bool IsExpanded
        public bool IsExpanded
        {
            get => _isExpanded;
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
                    RaisePropertyChanged(nameof(IsExpanded));
                }
            }
        }

        // 对照 WPF: public bool IsHidden
        public bool IsHidden
        {
            get => _isHidden;
            set
            {
                if (_isHidden != value)
                {
                    _isHidden = value;
                    RaisePropertyChanged(nameof(IsHidden));
                    ParentItem?.RaisePropertyChanged(nameof(ShowExpander));
                }
            }
        }

        // 对照 WPF: public bool IsSelected
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    RaisePropertyChanged(nameof(IsSelected));
                }
            }
        }

        // 对照 WPF: public ListBoxSelectionType SelectionType
        public ListBoxSelectionType SelectionType
        {
            get => _selectionType;
            set
            {
                if (_selectionType != value)
                {
                    _selectionType = value;
                    RaisePropertyChanged(nameof(SelectionType));
                }
            }
        }

        // 对照 WPF: public bool HasChildren
        public bool HasChildren => _children != null && _children.Count > 0;

        // 对照 WPF: public int Level
        public int Level => ParentItem == null ? 0 : ParentItem.Level + 1;

        // 对照 WPF: public virtual bool ShowExpander => (_children?.Count ?? 0) != 0;
        public virtual bool ShowExpander => (_children?.Count ?? 0) != 0;

        // 对照 WPF: public virtual bool IsFocusable => true;
        public virtual bool IsFocusable => true;

        // 对照 WPF: public string Title { get; set; }
        public string Title { get; set; }

        // 对照 WPF: public MultiselectionTreeViewItemCollection Children
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

        // 对照 WPF: public event PropertyChangedEventHandler PropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // 对照 WPF: protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 对照 WPF: public IEnumerable<MultiselectionTreeViewItem> Ancestors()
        public IEnumerable<MultiselectionTreeViewItem> Ancestors()
        {
            for (MultiselectionTreeViewItem node = ParentItem; node != null; node = node.ParentItem)
            {
                yield return node;
            }
        }

        // 对照 WPF: public MultiselectionTreeViewItem Previous()
        //   依赖 Flattener.IndexOf(this)，spike 版无 Flattener，返回 null
        public MultiselectionTreeViewItem Previous()
        {
            return null;
        }

        // 对照 WPF: public MultiselectionTreeViewItem Next()
        //   依赖 Flattener.IndexOf(this)，spike 版无 Flattener，返回 null
        public MultiselectionTreeViewItem Next()
        {
            return null;
        }

        // 对照 WPF: public void ApplyFilterToChild(MultiselectionTreeViewItem child, string filterString)
        //   bool flag = child.MatchFilter(filterString);
        //   if (child.HasChildren) { child.EnsureChildrenFiltered(filterString); child.IsHidden = child.AreAllChildrenHidden(); }
        //   else { child.IsHidden = !flag; }
        // spike 版保留：spike MultiselectionTreeViewItem 无 Flattener 依赖，简化版可直接复用
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

        // 对照 WPF: internal void EnsureChildrenFiltered(string filterString)
        internal void EnsureChildrenFiltered(string filterString)
        {
            foreach (MultiselectionTreeViewItem child in Children)
            {
                ApplyFilterToChild(child, filterString);
            }
        }

        // spike 版辅助：判断所有子节点是否都 IsHidden
        // 对照 WPF: private bool AreAllChildrenHidden()
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

        // 对照 WPF: protected virtual void OnExpanding() { }
        protected virtual void OnExpanding()
        {
        }

        // 对照 WPF: protected virtual void OnCollapsing() { }
        protected virtual void OnCollapsing()
        {
        }

        // 对照 WPF: protected virtual bool MatchFilter(string filterString)
        //   if (string.IsNullOrEmpty(filterString)) return true;
        //   if (Title.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) != -1) return true;
        //   return false;
        protected virtual bool MatchFilter(string filterString)
        {
            if (string.IsNullOrEmpty(filterString))
            {
                return true;
            }
            if (Title != null && Title.IndexOf(filterString, StringComparison.OrdinalIgnoreCase) != -1)
            {
                return true;
            }
            return false;
        }
    }
}
