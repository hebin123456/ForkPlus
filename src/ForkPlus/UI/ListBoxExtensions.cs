using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace ForkPlus.UI
{
	// 阶段 4.5：WPF System.Windows.Controls.ListBox + ItemContainerGenerator.ContainerFromIndex
	// → Avalonia ListBox + ItemContainerGenerator.ContainerFromIndex（API 兼容）。
	// WPF Keyboard.Focus → Avalonia Control.Focus。
	// WPF VisualTreeHelper.GetChild + ScrollViewer 嵌套查找
	// → Avalonia IVisual.GetVisualDescendants() + OfType<ScrollViewer>()。
	// WPF ListBox.ScrollIntoView(object) 在 Avalonia 11 中为 ListBox.ScrollIntoView(object)，
	// 但签名/行为可能不同；通过查找内部 ScrollViewer 直接设置偏移以保持原语义。
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
			// 阶段 4.5：WPF Keyboard.Focus(element) → Avalonia Control.Focus()。
			// MainWindow.Instance.IsActive 检查保留（Avalonia Window.IsActive 同样存在）。
			if (listBox.ItemContainerGenerator.ContainerFromIndex(row) is ListBoxItem element && MainWindow.Instance.IsActive)
			{
				element.Focus();
			}
		}

		private static void ScrollRowIntoView(ListBox listBox, int row)
		{
			// 阶段 4.5：WPF VisualTreeHelper.GetChild 嵌套查找 ScrollViewer
			// → Avalonia IVisual.GetVisualDescendants + OfType<ScrollViewer>()。
			// WPF ListBox 内部结构是 ListBox → Border → ScrollViewer；Avalonia 同样嵌套 ScrollViewer，
			// 但通过 GetVisualDescendants 查找更稳健（不依赖具体层级）。
			ScrollViewer scrollViewer = listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
			if (scrollViewer != null)
			{
				int num = ((row >= 1) ? (row - 1) : row);
				if (!((double)num > scrollViewer.Offset.Y) || !((double)num < scrollViewer.Offset.Y + scrollViewer.Viewport.Height))
				{
					scrollViewer.Offset = new Vector(scrollViewer.Offset.X, num);
				}
			}
		}
	}
}
