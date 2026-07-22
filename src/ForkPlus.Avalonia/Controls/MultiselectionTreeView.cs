using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 MultiselectionTreeView（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/MultiselectionTreeView.cs（544 行）：
    //   - WPF MultiselectionTreeView : ListView（WPF 用 ListView + Flattener 模拟 TreeView）
    //   - RootItemProperty (MultiselectionTreeViewItem) DependencyProperty
    //   - new IEnumerable ItemsSource { set { throw NotSupportedException("Use RootItem property instead"); } }
    //   - RememberExpandedItems / AllowDragDrop / FilterString / LastClickedItem 属性
    //   - Refilter / Expand / SelectAndFocus / FocusNode / HandleExpanding / ScrollIntoView
    //   - LockUpdates() → IDisposable UpdateLock
    //   - GetTopLevelSelection() → 过滤掉祖先已在选集中的项
    //   - WPF 拖放：OnDragEnter/Over/Leave/Drop + HandleDragEnter/Over/Leave/Drop（依赖 DragAdorner）
    //   - 静态构造：VirtualizingStackPanel.VirtualizationModeProperty.OverrideMetadata
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TreeView）：
    //   1. 基类 System.Windows.Controls.ListView → Avalonia.Controls.TreeView
    //      （task spec 明确要求：继承 TreeView；WPF 用 ListView + Flattener 模拟，
    //       spike 用 Avalonia 原生 TreeView，跳过 Flattener 扁平化逻辑）
    //   2. WPF DependencyProperty.Register → StyledProperty<T>.Register
    //   3. spike 跳过 Flattener（_flattener / _flattener_CollectionChanged / Reload）
    //   4. spike 跳过拖放（OnDragEnter/Over/Leave/Drop + HandleDrag* + DragAdorner）
    //   5. spike 跳过 VirtualizingStackPanel.VirtualizationMode OverrideMetadata
    //      （Avalonia 11 TreeView 默认有虚拟化，无需 OverrideMetadata）
    //   6. spike 跳过 GetContainerForItemOverride / IsItemItsOwnContainerOverride /
    //      PrepareContainerForItemOverride（Avalonia TreeView 用 TreeViewItem 而非 TreeViewControlItem）
    //   7. spike 跳过 OnSelectionChanged 同步 IsSelected（spike 用 TreeView 内置选择）
    //   8. spike 跳过 OnKeyDown Left/Right 折叠展开（spike 用 TreeView 内置键盘）
    //   9. spike 跳过 OnPreviewMouseRightButtonDown / OnMouseDoubleClick
    //      LastClickedItem 同步（spike 用 TreeView 内置鼠标）
    //  10. spike 跳过 FocusNode / HandleExpanding / ScrollIntoView(MultiselectionTreeViewItem)
    //      （依赖 ItemContainerGenerator.Status + DispatcherOperationCallback，spike 不实现）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TreeView
    //   - RootItem StyledProperty（替代 ItemsSource）
    //   - new IEnumerable ItemsSource { set { throw NotSupportedException; } }
    //   - RememberExpandedItems / AllowDragDrop / FilterString / LastClickedItem 属性
    //   - Refilter / Expand / LockUpdates / GetTopLevelSelection 方法
    public class MultiselectionTreeView : TreeView
    {
        // 对照 WPF: public static readonly DependencyProperty RootItemProperty
        public static readonly StyledProperty<MultiselectionTreeViewItem> RootItemProperty =
            AvaloniaProperty.Register<MultiselectionTreeView, MultiselectionTreeViewItem>(nameof(RootItem));

        // 对照 WPF: private string _filterString
        private string _filterString;

        // 对照 WPF: private bool _updatesLocked
        private bool _updatesLocked;

        // 对照 WPF: public MultiselectionTreeViewItem RootItem
        public MultiselectionTreeViewItem RootItem
        {
            get => GetValue(RootItemProperty);
            set => SetValue(RootItemProperty, value);
        }

        // 对照 WPF: public new IEnumerable ItemsSource { set { throw NotSupportedException("Use RootItem property instead"); } }
        // spike 版：屏蔽基类 ItemsSource setter，抛出 NotSupportedException 提示用 RootItem
        public new IEnumerable ItemsSource
        {
            get => base.ItemsSource;
            set => throw new NotSupportedException("Use RootItem property instead");
        }

        // 对照 WPF: public bool RememberExpandedItems { get; set; }
        public bool RememberExpandedItems { get; set; }

        // 对照 WPF: public bool AllowDragDrop { get; set; }
        public bool AllowDragDrop { get; set; }

        // 对照 WPF: public string FilterString
        public string FilterString
        {
            get => _filterString;
            set
            {
                if (_filterString != value)
                {
                    _filterString = value;
                    Refilter();
                    RootItem?.ExpandAllChildren();
                }
            }
        }

        // 对照 WPF: public MultiselectionTreeViewItem LastClickedItem { get; private set; }
        public MultiselectionTreeViewItem LastClickedItem { get; private set; }

        // 对照 WPF: public void Refilter()
        //   MultiselectionTreeViewItem rootItem = RootItem;
        //   if (rootItem == null) return;
        //   foreach (MultiselectionTreeViewItem child in rootItem.Children)
        //       rootItem.ApplyFilterToChild(child, _filterString);
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

        // 对照 WPF: public void Expand(MultiselectionTreeViewItem node, bool expandChildren)
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

        // 对照 WPF: public void SelectAndFocus(MultiselectionTreeViewItem node)
        //   base.SelectedItems.Add(node); if (base.IsFocused) FocusNode(node);
        // spike 版简化：spike 不实现 FocusNode，仅设置 SelectedItem
        public void SelectAndFocus(MultiselectionTreeViewItem node)
        {
            SelectedItem = node;
        }

        // 对照 WPF: public IDisposable LockUpdates() { return new UpdateLock(this); }
        public IDisposable LockUpdates()
        {
            return new UpdateLock(this);
        }

        // 对照 WPF: public IEnumerable<MultiselectionTreeViewItem> GetTopLevelSelection()
        //   过滤掉祖先已在选集中的项
        // spike 版简化：用 SelectedItems 遍历（Avalonia TreeView 多选支持）
        public IEnumerable<MultiselectionTreeViewItem> GetTopLevelSelection()
        {
            // spike 版：Avalonia TreeView.SelectedItems 为 IList<object?>
            var selection = SelectedItems;
            if (selection == null || selection.Count == 0)
            {
                yield break;
            }
            HashSet<MultiselectionTreeViewItem> selectionHash = new HashSet<MultiselectionTreeViewItem>();
            foreach (var item in selection)
            {
                if (item is MultiselectionTreeViewItem msi)
                {
                    selectionHash.Add(msi);
                }
            }
            foreach (MultiselectionTreeViewItem item in selectionHash)
            {
                bool ancestorSelected = false;
                foreach (MultiselectionTreeViewItem ancestor in item.Ancestors())
                {
                    if (selectionHash.Contains(ancestor))
                    {
                        ancestorSelected = true;
                        break;
                    }
                }
                if (!ancestorSelected)
                {
                    yield return item;
                }
            }
        }

        // 对照 WPF: OnPropertyChanged(DependencyPropertyChangedEventArgs e) → if (RootItem) Reload()
        // spike 版：RootItem 变化时不重新加载（spike 用 TreeView 内置 ItemsSource 绑定）
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == RootItemProperty)
            {
                // spike 版：直接把 RootItem 包装为单元素数组绑定到 ItemsSource
                // （WPF 用 Flattener 扁平化，spike 用 TreeView 原生层级展示）
                if (RootItem != null)
                {
                    var list = new List<MultiselectionTreeViewItem> { RootItem };
                    base.ItemsSource = list;
                }
                else
                {
                    base.ItemsSource = null;
                }
            }
        }

        // 对照 WPF: private class UpdateLock : IDisposable
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
    }
}
