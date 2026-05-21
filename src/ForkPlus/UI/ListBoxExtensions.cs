using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
			(listbox.ItemContainerGenerator.ContainerFromIndex(row) as ListBoxItem)?.Focus();
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
			if (listBox.ItemContainerGenerator.ContainerFromIndex(row) is ListBoxItem element && MainWindow.Instance.IsActive)
			{
				Keyboard.Focus(element);
			}
		}

		private static void ScrollRowIntoView(ListBox listBox, int row)
		{
			if (VisualTreeHelper.GetChildrenCount(listBox) != 0)
			{
				ScrollViewer scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild((Border)VisualTreeHelper.GetChild(listBox, 0), 0);
				int num = ((row >= 1) ? (row - 1) : row);
				if (!((double)num > scrollViewer.VerticalOffset) || !((double)num < scrollViewer.VerticalOffset + scrollViewer.ViewportHeight))
				{
					scrollViewer.ScrollToVerticalOffset(num);
				}
			}
		}
	}
}
