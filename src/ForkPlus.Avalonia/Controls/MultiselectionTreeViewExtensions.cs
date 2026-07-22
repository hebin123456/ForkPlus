using System.Collections.Generic;
using ForkPlus.UI.Controls;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 MultiselectionTreeViewExtensions（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/MultiselectionTreeViewExtensions.cs（109 行）：
    //   - WPF MultiselectionTreeViewExtensions : static class
    //   - GetExpandedItems(this MultiselectionTreeView) → ExpandedTreeViewElement[]
    //     （递归收集 RootItem 下所有 IsExpanded 的子节点 Title 树）
    //   - SetExpandedItems(this MultiselectionTreeView, ExpandedTreeViewElement[])
    //     （按 Title 匹配展开子节点）
    //   - CollapseAllChildren(this MultiselectionTreeViewItem)
    //   - ExpandAllChildren(this MultiselectionTreeViewItem)
    //   - RefreshSelectionType(this MultiselectionTreeViewItem[])
    //     （根据 Previous/Next 是否在选集中计算 Top/Middle/Bottom/Separate）
    //   - private SetExpandedItems(treeViewItem, childToExpand)
    //   - private GetExpandedItems(item) → ExpandedTreeViewElement
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   2. ExpandedTreeViewElement 来自 ForkPlus.Core.UI.Controls（已存在）
    //   3. IListExtensions.ContainsItem 来自 ForkPlus.Core
    //   4. spike MultiselectionTreeViewItem.Previous()/Next() 返回 null，
    //      所以 RefreshSelectionType 会把所有项设为 Separate（spike 行为可接受）
    //   5. spike MultiselectionTreeView.RootItem 由 spike MultiselectionTreeView 提供
    //
    // spike 简化：
    //   - 与 WPF 一致的扩展方法签名 + 算法逻辑
    public static class MultiselectionTreeViewExtensions
    {
        // 对照 WPF: [Null] public static ExpandedTreeViewElement[] GetExpandedItems(this MultiselectionTreeView treeView)
        public static ExpandedTreeViewElement[] GetExpandedItems(this MultiselectionTreeView treeView)
        {
            MultiselectionTreeViewItem rootItem = treeView.RootItem;
            if (rootItem != null)
            {
                return GetExpandedItems(rootItem).Children;
            }
            return null;
        }

        // 对照 WPF: public static void SetExpandedItems(this MultiselectionTreeView treeView, [Null] ExpandedTreeViewElement[] rootExpandedItems)
        public static void SetExpandedItems(this MultiselectionTreeView treeView, ExpandedTreeViewElement[] rootExpandedItems)
        {
            if (rootExpandedItems != null)
            {
                foreach (ExpandedTreeViewElement childToExpand in rootExpandedItems)
                {
                    SetExpandedItems(treeView.RootItem, childToExpand);
                }
            }
        }

        // 对照 WPF: public static void CollapseAllChildren(this MultiselectionTreeViewItem item)
        public static void CollapseAllChildren(this MultiselectionTreeViewItem item)
        {
            foreach (MultiselectionTreeViewItem child in item.Children)
            {
                child.CollapseAllChildren();
                if (child.HasChildren)
                {
                    child.IsExpanded = false;
                }
            }
        }

        // 对照 WPF: public static void ExpandAllChildren(this MultiselectionTreeViewItem item)
        public static void ExpandAllChildren(this MultiselectionTreeViewItem item)
        {
            foreach (MultiselectionTreeViewItem child in item.Children)
            {
                child.ExpandAllChildren();
                if (child.HasChildren)
                {
                    child.IsExpanded = true;
                }
            }
        }

        // 对照 WPF: public static void RefreshSelectionType(this MultiselectionTreeViewItem[] items)
        //   根据 Previous/Next 是否在选集中计算 Top/Middle/Bottom/Separate
        // spike 版：Previous()/Next() 返回 null，所有项都会被设为 Separate
        public static void RefreshSelectionType(this MultiselectionTreeViewItem[] items)
        {
            foreach (MultiselectionTreeViewItem multiselectionTreeViewItem in items)
            {
                MultiselectionTreeViewItem multiselectionTreeViewItem2 = multiselectionTreeViewItem.Previous();
                MultiselectionTreeViewItem multiselectionTreeViewItem3 = multiselectionTreeViewItem.Next();
                bool flag = multiselectionTreeViewItem2 != null && items.ContainsItem(multiselectionTreeViewItem2);
                bool flag2 = multiselectionTreeViewItem3 != null && items.ContainsItem(multiselectionTreeViewItem3);
                if (flag && flag2)
                {
                    multiselectionTreeViewItem.SelectionType = ListBoxSelectionType.Middle;
                }
                else if (flag)
                {
                    multiselectionTreeViewItem.SelectionType = ListBoxSelectionType.Bottom;
                }
                else if (flag2)
                {
                    multiselectionTreeViewItem.SelectionType = ListBoxSelectionType.Top;
                }
                else
                {
                    multiselectionTreeViewItem.SelectionType = ListBoxSelectionType.Separate;
                }
            }
        }

        // 对照 WPF: private static void SetExpandedItems(MultiselectionTreeViewItem treeViewItem, ExpandedTreeViewElement childToExpand)
        private static void SetExpandedItems(MultiselectionTreeViewItem treeViewItem, ExpandedTreeViewElement childToExpand)
        {
            foreach (MultiselectionTreeViewItem child in treeViewItem.Children)
            {
                if (child.Title == childToExpand.Name)
                {
                    child.IsExpanded = true;
                    ExpandedTreeViewElement[] children = childToExpand.Children;
                    foreach (ExpandedTreeViewElement childToExpand2 in children)
                    {
                        SetExpandedItems(child, childToExpand2);
                    }
                    break;
                }
            }
        }

        // 对照 WPF: private static ExpandedTreeViewElement GetExpandedItems(MultiselectionTreeViewItem item)
        private static ExpandedTreeViewElement GetExpandedItems(MultiselectionTreeViewItem item)
        {
            List<ExpandedTreeViewElement> list = new List<ExpandedTreeViewElement>();
            foreach (MultiselectionTreeViewItem child in item.Children)
            {
                if (child.IsExpanded)
                {
                    list.Add(GetExpandedItems(child));
                }
            }
            return new ExpandedTreeViewElement(item.Title, list.ToArray());
        }
    }
}
