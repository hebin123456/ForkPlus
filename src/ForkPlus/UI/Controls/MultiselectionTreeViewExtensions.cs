using System.Collections.Generic;

namespace ForkPlus.UI.Controls
{
	public static class MultiselectionTreeViewExtensions
	{
		[Null]
		public static ExpandedTreeViewElement[] GetExpandedItems(this MultiselectionTreeView treeView)
		{
			MultiselectionTreeViewItem rootItem = treeView.RootItem;
			if (rootItem != null)
			{
				return GetExpandedItems(rootItem).Children;
			}
			return null;
		}

		public static void SetExpandedItems(this MultiselectionTreeView treeView, [Null] ExpandedTreeViewElement[] rootExpandedItems)
		{
			if (rootExpandedItems != null)
			{
				foreach (ExpandedTreeViewElement childToExpand in rootExpandedItems)
				{
					SetExpandedItems(treeView.RootItem, childToExpand);
				}
			}
		}

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
