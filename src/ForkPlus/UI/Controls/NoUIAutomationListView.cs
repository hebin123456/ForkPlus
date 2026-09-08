using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// WPF 版通过返回 StubWindowAutomationPeer 屏蔽 UI Automation 暴露（性能优化）。
	/// Avalonia 无 FrameworkElementAutomationPeer 等价 API，已随迁移移除。
	/// Migration note：UpdateResizableColumnWidth 依赖 WPF ListView+GridView 列宽模型，
	/// Avalonia ListBox 无 GridView；该方法暂为 no-op，列宽自适应待重新设计。
	/// </summary>
	public class NoUIAutomationListView : global::Avalonia.Controls.ListBox
	{
		public enum SelectOptions
		{
			None,
			ScrollIntoView,
			Focus
		}

		public bool IsMultiselectionInProgress { get; set; }

		/// <summary>
		/// WPF PreviewMouseWheel（隧道路由预览滚轮事件）的兼容入口。
		/// Avalonia 无独立 Preview 事件，等价映射为 Tunnel 路由的 PointerWheelChanged，
		/// 供 SubmoduleDiffUserControl 等旧代码用 +=/-= 语法订阅（其 add 侧即 AddHandler+Tunnel）。
		/// </summary>
		public event global::System.EventHandler<PointerWheelEventArgs> PreviewMouseWheel
		{
			add
			{
				AddHandler(global::Avalonia.Input.InputElement.PointerWheelChangedEvent, value, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
			}
			remove
			{
				RemoveHandler(global::Avalonia.Input.InputElement.PointerWheelChangedEvent, value);
			}
		}

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
			List<int> validRows = new List<int>();
			for (int i = 0; i < rows.Count; i++)
			{
				int row = rows[i];
				if (row >= 0 && row < base.ItemCount)
				{
					validRows.Add(row);
				}
			}
			if (validRows.Count == 0)
			{
				return;
			}
			IsMultiselectionInProgress = true;
			try
			{
				base.SelectedItems.Clear();
				for (int i = 0; i < validRows.Count; i++)
				{
					object item = base.Items[validRows[i]];
					if (!base.SelectedItems.Contains(item))
					{
						base.SelectedItems.Add(item);
					}
				}
				base.SelectedIndex = validRows[0];
				base.SelectedItem = base.Items[validRows[0]];
			}
			finally
			{
				IsMultiselectionInProgress = false;
			}
			if ((options & SelectOptions.ScrollIntoView) != 0)
			{
				ScrollRowIntoView(this, validRows[0]);
			}
			ApplyContainerSelection(validRows);
			if ((options & SelectOptions.Focus) != 0)
			{
				SetKeyboardFocus(this, validRows[0]);
			}
		}

		private void ApplyContainerSelection(IReadOnlyList<int> rows)
		{
			bool missingContainer = false;
			for (int i = 0; i < rows.Count; i++)
			{
				int row = rows[i];
				if (base.ContainerFromIndex(row) is ListBoxItem item)
				{
					item.IsSelected = true;
					item.InvalidateVisual();
				}
				else
				{
					missingContainer = true;
				}
			}
			if (missingContainer)
			{
				Dispatcher.UIThread.Post(delegate
				{
					for (int i = 0; i < rows.Count; i++)
					{
						if (base.ContainerFromIndex(rows[i]) is ListBoxItem item)
						{
							item.IsSelected = true;
							item.InvalidateVisual();
						}
					}
				}, DispatcherPriority.Background);
			}
		}

		private static void ScrollRowIntoView(ListBox listBox, int row)
		{
			// WPF 版沿视觉树找内嵌 ScrollViewer 控制偏移；Avalonia ListBox 自带
			// ScrollIntoView（生成容器并滚动到位），直接使用。
			listBox.ScrollIntoView(listBox.Items[row < listBox.ItemCount ? row : listBox.ItemCount - 1]);
		}

		private static void SetKeyboardFocus(ListBox listBox, int row)
		{
			if (row >= 0 && row < listBox.ItemCount && MainWindow.Instance.IsActive
				&& listBox.ContainerFromIndex(row) is ListBoxItem element)
			{
				element.Focus();
			}
		}

		public void UpdateResizableColumnWidth(int resizableColumnIndex)
		{
			// no-op：见类注释 Migration note
		}
	}
}
