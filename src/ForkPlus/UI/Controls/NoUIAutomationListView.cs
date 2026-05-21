using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ForkPlus.UI.Controls
{
	public class NoUIAutomationListView : ListView
	{
		public enum SelectOptions
		{
			None,
			ScrollIntoView,
			Focus
		}

		public class StubWindowAutomationPeer : FrameworkElementAutomationPeer
		{
			public StubWindowAutomationPeer(FrameworkElement owner)
				: base(owner)
			{
			}

			protected override string GetNameCore()
			{
				return "StubWindowAutomationPeer";
			}

			protected override AutomationControlType GetAutomationControlTypeCore()
			{
				return AutomationControlType.Window;
			}

			protected override List<AutomationPeer> GetChildrenCore()
			{
				return new List<AutomationPeer>();
			}
		}

		public bool IsMultiselectionInProgress { get; set; }

		public double AvailableWidth => base.ActualWidth - 15.0 - 4.0 - 4.0;

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

		private static void SetKeyboardFocus(ListBox listBox, int row)
		{
			listBox.UpdateLayout();
			if (listBox.ItemContainerGenerator.ContainerFromIndex(row) is ListBoxItem element && MainWindow.Instance.IsActive)
			{
				Keyboard.Focus(element);
			}
		}

		protected override AutomationPeer OnCreateAutomationPeer()
		{
			return new StubWindowAutomationPeer(this);
		}

		public void UpdateResizableColumnWidth(int resizableColumnIndex)
		{
			GridView gridView = base.View as GridView;
			double num = 0.0;
			for (int i = 0; i < gridView.Columns.Count; i++)
			{
				if (i != resizableColumnIndex)
				{
					num += gridView.Columns[i].ActualWidth;
				}
			}
			double num2 = AvailableWidth - num;
			gridView.Columns[resizableColumnIndex].Width = ((num2 > 0.0) ? num2 : 0.0);
		}
	}
}
