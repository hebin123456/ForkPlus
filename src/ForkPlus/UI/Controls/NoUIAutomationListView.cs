// 阶段 4.5：WPF System.Windows.* → Avalonia.* 迁移。
// WPF ListView → Avalonia.Controls.ListView。
// WPF AutomationPeer（UI 自动化桩）→ Avalonia 内置自动化系统，无需自定义 stub。
// WPF GridView（ListView 多列视图）在 Avalonia 中无等价物。
// WPF VisualTreeHelper → Avalonia GetVisualDescendants。
// WPF Keyboard.Focus → Avalonia Control.Focus。
// WPF ItemContainerGenerator.ContainerFromIndex → Avalonia ItemsControl.ContainerFromIndex。
// WPF FrameworkElement.ActualWidth → Avalonia Layoutable.Bounds.Width。
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ForkPlus.UI.Controls
{
	public class NoUIAutomationListView : Avalonia.Controls.ListView
	{
		public enum SelectOptions
		{
			None,
			ScrollIntoView,
			Focus
		}

		// 阶段 4.5：WPF AutomationPeer（UI 自动化桩）→ Avalonia 内置自动化系统，无需自定义 stub。
		// 原 StubWindowAutomationPeer 内部类及 OnCreateAutomationPeer() 重写已移除。

		public bool IsMultiselectionInProgress { get; set; }

		// 阶段 4.5：WPF FrameworkElement.ActualWidth → Avalonia Layoutable.Bounds.Width。
		public double AvailableWidth => base.Bounds.Width - 15.0 - 4.0 - 4.0;

		public void Select(int row, SelectOptions options = (SelectOptions)3)
		{
			Select(new int[1] { row }, options);
		}

		public void Select(IReadOnlyList<int> rows, SelectOptions options = (SelectOptions)3)
		{
			if (rows.Count == 0)
			{
				return;
			}
			IsMultiselectionInProgress = true;
			base.SelectedItems.Clear();
			for (int i = 0; i < rows.Count; i++)
			{
				if (i == rows.Count - 1)
				{
					IsMultiselectionInProgress = false;
				}
				base.SelectedItems.Add(base.Items[rows[i]]);
			}
			if ((options & SelectOptions.ScrollIntoView) != 0)
			{
				ScrollRowIntoView(this, rows[0]);
			}
			if ((options & SelectOptions.Focus) != 0)
			{
				SetKeyboardFocus(this, rows[0]);
			}
		}

		private static void ScrollRowIntoView(ListBox listBox, int row)
		{
			// 阶段 4.5：WPF VisualTreeHelper.GetChildrenCount/GetChild 逐层查找 Border→ScrollViewer
			// → Avalonia GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() 一次性查找。
			ScrollViewer scrollViewer = listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
			if (scrollViewer != null)
			{
				int num = ((row >= 1) ? (row - 1) : row);
				// TODO(4.5): 验证 Avalonia ScrollViewer.Offset.Y 替代 WPF VerticalOffset。
				// TODO(4.5): 验证 Avalonia ScrollViewer.Viewport.Height 替代 WPF ViewportHeight。
				if (!((double)num > scrollViewer.Offset.Y) || !((double)num < scrollViewer.Offset.Y + scrollViewer.Viewport.Height))
				{
					// 阶段 4.5：WPF ScrollViewer.ScrollToVerticalOffset(num) → Avalonia 直接设置 Offset（Vector）。
					scrollViewer.Offset = new Vector(scrollViewer.Offset.X, num);
				}
			}
		}

		private static void SetKeyboardFocus(ListBox listBox, int row)
		{
			// 阶段 4.5：Avalonia Layoutable.UpdateLayout() 与 WPF 同名方法存在。
			listBox.UpdateLayout();
			// 阶段 4.5：WPF ItemContainerGenerator.ContainerFromIndex → Avalonia ItemsControl.ContainerFromIndex。
			// TODO(4.5): 验证 Avalonia ContainerFromIndex 返回类型
			if (listBox.ContainerFromIndex(row) is ListBoxItem element && MainWindow.Instance.IsActive)
			{
				// 阶段 4.5：WPF Keyboard.Focus(element) → Avalonia Control.Focus()。
				element.Focus();
			}
		}

		// TODO(4.5): WPF GridView（ListView 多列视图）在 Avalonia 中无等价物。UpdateResizableColumnWidth 功能需改用 DataGrid 或自定义列布局实现。
		public void UpdateResizableColumnWidth(int resizableColumnIndex)
		{
			// 阶段 4.5：WPF GridView 在 Avalonia 中无等价物，方法体已禁用。
			// GridView gridView = base.View as GridView;
			// double num = 0.0;
			// for (int i = 0; i < gridView.Columns.Count; i++)
			// {
			//     if (i != resizableColumnIndex)
			//     {
			//         num += gridView.Columns[i].ActualWidth;
			//     }
			// }
			// double num2 = AvailableWidth - num;
			// gridView.Columns[resizableColumnIndex].Width = ((num2 > 0.0) ? num2 : 0.0);
		}
	}
}
