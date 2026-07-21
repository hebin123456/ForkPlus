using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ClosableTabControl（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ClosableTabControl.cs（134 行）：
    //   - WPF ClosableTabControl : TabControl
    //   - [TemplatePart] PART_Add (Button)
    //   - AddButtonClicked / TabItemRemoved / SelectedTabItemChanged 事件
    //   - SelectedTab 属性：SelectedItem as ClosableTabItem
    //   - StopSelectionChangedEventWhileDropInProgress 标志
    //   - AddTab / RemoveTab / RemoveAllTabs(exceptItem) / SelectTab / SelectNextTab /
    //     SelectPreviousTab / InsertAt / IndexOf
    //   - OnApplyTemplate：获取 PART_Add button + 订阅 Click
    //   - OnSelectionChanged：触发 SelectedTabItemChanged（除非 drop in progress）
    //   - RemoveTab：若移除的是选中项，先选前一项；空时 NewTab.Execute()
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TabControl + 添加关闭按钮）：
    //   1. 基类 TabControl → Avalonia.Controls.TabControl（API 一致）
    //   2. WPF base.Items.Add / Items.Remove → Avalonia Items 集合（用 IList 兼容）
    //   3. WPF base.Items.IsEmpty → spike 用 Items.Count == 0
    //   4. WPF base.Items.CompactMap → spike 用 LINQ OfType<T>()
    //   5. WPF MainWindow.Commands.NewTab.Execute() → spike 跳过（无 MainWindow.Commands 依赖）
    //   6. WPF [TemplatePart] PART_Add → spike 跳过 OnApplyTemplate，
    //      用 AddButtonClicked 事件由外部触发
    //   7. WPF OnSelectionChanged (SelectionChangedEventArgs) → Avalonia SelectionChanged 事件
    //   8. WPF EventArgs<ClosableTabItem> → spike 用 EventArgs<ClosableTabItem> 本地类型
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TabControl + AddTab/RemoveTab/SelectTab/SelectNextTab/SelectPreviousTab
    //   - AddButtonClicked / TabItemRemoved / SelectedTabItemChanged 事件
    //   - SelectedTab 属性
    public class ClosableTabControl : TabControl
    {
        // 对照 WPF: public EventHandler AddButtonClicked
        public event EventHandler AddButtonClicked;

        // 对照 WPF: public EventHandler TabItemRemoved
        public event EventHandler TabItemRemoved;

        // 对照 WPF: public EventHandler<EventArgs<ClosableTabItem>> SelectedTabItemChanged
        public event EventHandler<EventArgs<ClosableTabItem>> SelectedTabItemChanged;

        // 对照 WPF: public ClosableTabItem SelectedTab => base.SelectedItem as ClosableTabItem
        public ClosableTabItem SelectedTab => SelectedItem as ClosableTabItem;

        // 对照 WPF: public bool StopSelectionChangedEventWhileDropInProgress { get; set; }
        public bool StopSelectionChangedEventWhileDropInProgress { get; set; }

        public ClosableTabControl()
        {
            // 对照 WPF: OnSelectionChanged (SelectionChangedEventArgs e)
            // spike 版：订阅 SelectionChanged 事件
            SelectionChanged += ClosableTabControl_SelectionChanged;
        }

        // 对照 WPF: public void AddTab(ClosableTabItem tab)
        //   base.Items.Add(tab);
        public void AddTab(ClosableTabItem tab)
        {
            Items.Add(tab);
        }

        // 对照 WPF: public void RemoveTab(ClosableTabItem tab)
        //   if (tab.IsSelected) { ... } base.Items.Remove(tab); TabItemRemoved?.Invoke();
        //   if (base.Items.IsEmpty) MainWindow.Commands.NewTab.Execute();
        public void RemoveTab(ClosableTabItem tab)
        {
            if (tab != null && tab.IsSelected)
            {
                int num = SelectedIndex - 1;
                if (num >= 0)
                {
                    SelectedIndex = num;
                }
            }
            Items.Remove(tab);
            TabItemRemoved?.Invoke(this, EventArgs.Empty);
            // 对照 WPF: if (base.Items.IsEmpty) MainWindow.Commands.NewTab.Execute()
            // spike 版跳过（无 MainWindow.Commands 依赖）
        }

        // 对照 WPF: public void RemoveAllTabs(ClosableTabItem exceptItem = null)
        public void RemoveAllTabs(ClosableTabItem exceptItem = null)
        {
            ClosableTabItem[] array = Items.OfType<ClosableTabItem>().ToArray();
            if (exceptItem != null)
            {
                exceptItem.IsSelected = true;
            }
            foreach (ClosableTabItem item in array)
            {
                if (!ReferenceEquals(exceptItem, item))
                {
                    Items.Remove(item);
                }
            }
            TabItemRemoved?.Invoke(this, EventArgs.Empty);
        }

        // 对照 WPF: public void SelectTab(ClosableTabItem itemToSelect)
        //   base.SelectedItem = itemToSelect;
        public void SelectTab(ClosableTabItem itemToSelect)
        {
            SelectedItem = itemToSelect;
        }

        // 对照 WPF: public void SelectNextTab()
        //   int num = base.SelectedIndex + 1; if (num == base.Items.Count) num = 0;
        //   base.SelectedIndex = num;
        public void SelectNextTab()
        {
            int num = SelectedIndex + 1;
            if (num == Items.Count)
            {
                num = 0;
            }
            SelectedIndex = num;
        }

        // 对照 WPF: public void SelectPreviousTab()
        //   int num = base.SelectedIndex - 1; if (num == -1) num = base.Items.Count - 1;
        //   base.SelectedIndex = num;
        public void SelectPreviousTab()
        {
            int num = SelectedIndex - 1;
            if (num == -1)
            {
                num = Items.Count - 1;
            }
            SelectedIndex = num;
        }

        // 对照 WPF: public void InsertAt(ClosableTabItem item, int index)
        //   base.Items.Insert(index, item);
        public void InsertAt(ClosableTabItem item, int index)
        {
            Items.Insert(index, item);
        }

        // 对照 WPF: public int IndexOf(ClosableTabItem item)
        //   return base.Items.IndexOf(item);
        public int IndexOf(ClosableTabItem item)
        {
            return Items.IndexOf(item);
        }

        // 对照 WPF: protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        //   base.OnSelectionChanged(e);
        //   if (!StopSelectionChangedEventWhileDropInProgress) {
        //     SelectedTabItemChanged?.Invoke(this, new EventArgs<ClosableTabItem>(base.SelectedItem as ClosableTabItem)); }
        private void ClosableTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!StopSelectionChangedEventWhileDropInProgress)
            {
                SelectedTabItemChanged?.Invoke(this, new EventArgs<ClosableTabItem>(SelectedItem as ClosableTabItem));
            }
        }

        // 对照 WPF: private void AddButton_Clicked(object sender, RoutedEventArgs e)
        //   AddButtonClicked?.Invoke(this, EventArgs.Empty);
        // spike 版：公共方法触发 AddButtonClicked（外部按钮调用）
        public void RaiseAddButtonClicked()
        {
            AddButtonClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    // spike 版 EventArgs<T>（替代 WPF ForkPlus.EventArgs<T>）
    public class EventArgs<T> : EventArgs
    {
        public T Value { get; }

        public EventArgs(T value)
        {
            Value = value;
        }
    }
}
