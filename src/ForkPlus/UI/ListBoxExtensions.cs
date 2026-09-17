using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace ForkPlus.UI
{
	public static class ListBoxExtensions
	{
		public enum SelectOptions
		{
			None,
			ScrollIntoView,
			Focus
		}

		private enum Direction
		{
			Forward = 1,
			Backward = -1
		}

		public static void SelectRow(this ListBox listBox, int row, SelectOptions options = (SelectOptions)3)
		{
			listBox.SelectedIndex = row;
			if ((options & SelectOptions.ScrollIntoView) != 0)
			{
				ScrollRowIntoView(listBox, row);
			}
			if ((options & SelectOptions.Focus) != 0)
			{
				SetKeyboardFocus(listBox, row);
			}
		}

		public static void SelectAndScrollIntoView(this ListBox listBox, int row, bool focus = true)
		{
			listBox.SelectedIndex = row;
			ScrollRowIntoView(listBox, row);
			if (focus)
			{
				SetKeyboardFocus(listBox, row);
			}
		}

		public static bool SelectNextRow(this ListBox listBox, int row, bool loop, [Null] Func<object, bool> condition = null)
		{
			bool flag = listBox.SelectNextRow(row, Direction.Forward, condition);
			if (!flag && loop)
			{
				return listBox.SelectNextRow(-1, Direction.Forward, condition);
			}
			return flag;
		}

		public static bool SelectPreviousRow(this ListBox listBox, int row, bool loop, [Null] Func<object, bool> condition = null)
		{
			bool flag = listBox.SelectNextRow(row, Direction.Backward, condition);
			if (!flag && loop)
			{
				return listBox.SelectNextRow(listBox.Items.Count, Direction.Backward, condition);
			}
			return flag;
		}

		public static void FocusRow(this ListBox listbox, int row)
		{
			(listbox.ContainerFromIndex(row) as ListBoxItem)?.Focus();
		}

		private static bool SelectNextRow(this ListBox listBox, int row, Direction direction, [Null] Func<object, bool> condition)
		{
			for (int i = (int)(row + direction); i >= 0 && i < listBox.Items.Count; i = (int)(i + direction))
			{
				if (condition == null || condition(listBox.Items[i]))
				{
					listBox.SelectedIndex = i;
					listBox.ScrollIntoView(listBox.SelectedItem);
					return true;
				}
			}
			return false;
		}

		private static void SetKeyboardFocus(ListBox listBox, int row)
		{
			listBox.UpdateLayout();
			if (listBox.ContainerFromIndex(row) is ListBoxItem element && MainWindow.Instance.IsActive)
			{
				(element).Focus();
			}
		}

		private static void ScrollRowIntoView(ListBox listBox, int row)
		{
			// 修复（2026-09-17，Ctrl+F 提交搜索跳转滚不到匹配行）：WPF 版把行号直接当
			// ScrollViewer 偏移（WPF 虚拟化 ListBox 为逻辑滚动，偏移单位即行号）；Avalonia
			// ScrollViewer.Offset 恒为像素，行号当像素用只会滚到列表顶部附近，匹配行永远
			// 不进视口。改用 ListBox.ScrollIntoView（实化容器并滚动到位），与
			// NoUIAutomationListView.ScrollRowIntoView 的迁移做法一致。
			if (row >= 0 && row < listBox.ItemCount)
			{
				listBox.ScrollIntoView(listBox.Items[row]);
			}
		}
	}
}
