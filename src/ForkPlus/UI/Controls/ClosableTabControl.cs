using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF System.Windows.Controls → Avalonia.Controls。
	// WPF ItemCollection.IsEmpty → Avalonia ItemCollection.Count == 0（无 IsEmpty 属性）。
	public class ClosableTabControl : TabControl
	{
		private const string AddButton = "PART_Add";

		public EventHandler AddButtonClicked;

		public EventHandler TabItemRemoved;

		public EventHandler<EventArgs<ClosableTabItem>> SelectedTabItemChanged;

		[Null]
		public ClosableTabItem SelectedTab => base.SelectedItem as ClosableTabItem;

		public bool StopSelectionChangedEventWhileDropInProgress { get; set; }

	// 阶段 5：Avalonia SelectingItemsControl.OnSelectionChanged 非虚方法，改为订阅 SelectionChanged 事件。
	public ClosableTabControl()
	{
		this.SelectionChanged += OnSelectionChanged;
	}

	public void AddTab(ClosableTabItem tab)
		{
			base.Items.Add(tab);
		}

		public void RemoveTab(ClosableTabItem tab)
		{
			if (tab.IsSelected)
			{
				int num = base.SelectedIndex - 1;
				if (num >= 0)
				{
					base.SelectedIndex = num;
				}
			}
			base.Items.Remove(tab);
			TabItemRemoved?.Invoke(this, null);
			// 阶段 4.5：WPF ItemCollection.IsEmpty → Avalonia Count == 0。
			if (base.Items.Count == 0)
			{
				MainWindow.Commands.NewTab.Execute();
			}
		}

		public void RemoveAllTabs(ClosableTabItem exceptItem = null)
		{
			ClosableTabItem closableTabItem = null;
			ClosableTabItem[] array = base.Items.CompactMap((object x) => x as ClosableTabItem);
			if (exceptItem != null)
			{
				exceptItem.IsSelected = true;
			}
			else
			{
				closableTabItem = new ClosableTabItem();
				base.Items.Add(closableTabItem);
				closableTabItem.IsSelected = true;
			}
			foreach (ClosableTabItem closableTabItem2 in array)
			{
				if (exceptItem != closableTabItem2)
				{
					base.Items.Remove(closableTabItem2);
				}
			}
			if (closableTabItem != null)
			{
				base.Items.Remove(closableTabItem);
			}
			TabItemRemoved?.Invoke(this, null);
			// 阶段 4.5：WPF ItemCollection.IsEmpty → Avalonia Count == 0。
			if (base.Items.Count == 0)
			{
				MainWindow.Commands.NewTab.Execute();
			}
		}

		// 阶段 5：Avalonia OnApplyTemplate 签名为 protected override OnApplyTemplate(TemplateAppliedEventArgs e)。
	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			if (e.NameScope?.Find("PART_Add") is Button button)
			{
				button.Click += AddButton_Clicked;
			}
		}

		public void SelectTab(ClosableTabItem itemToSelect)
		{
			base.SelectedItem = itemToSelect;
		}

		public void SelectNextTab()
		{
			int num = base.SelectedIndex + 1;
			if (num == base.Items.Count)
			{
				num = 0;
			}
			base.SelectedIndex = num;
		}

		public void SelectPreviousTab()
		{
			int num = base.SelectedIndex - 1;
			if (num == -1)
			{
				num = base.Items.Count - 1;
			}
			base.SelectedIndex = num;
		}

		public void InsertAt(ClosableTabItem item, int index)
		{
			base.Items.Insert(index, item);
		}

		public int IndexOf(ClosableTabItem item)
		{
			return base.Items.IndexOf(item);
		}

		// 阶段 5：Avalonia SelectingItemsControl.OnSelectionChanged 非虚方法，改为 SelectionChanged 事件处理器。
	private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!StopSelectionChangedEventWhileDropInProgress)
			{
				SelectedTabItemChanged?.Invoke(this, new EventArgs<ClosableTabItem>(base.SelectedItem as ClosableTabItem));
			}
		}

		private void AddButton_Clicked(object sender, RoutedEventArgs e)
		{
			AddButtonClicked?.Invoke(this, EventArgs.Empty);
		}
	}
}
