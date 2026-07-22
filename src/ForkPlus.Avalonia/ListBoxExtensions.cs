using System;
using Avalonia.Controls;

namespace ForkPlus.Avalonia
{
    // spike：从 WPF 工程 src/ForkPlus/UI/ListBoxExtensions.cs（106 行）迁移。
    //
    // 对照 WPF 源（namespace ForkPlus.UI）：
    //   - enum SelectOptions { None, ScrollIntoView, Focus }
    //   - enum Direction { Forward=1, Backward=-1 }
    //   - SelectRow(this ListBox, int, SelectOptions) → SelectedIndex + ScrollRowIntoView + SetKeyboardFocus
    //   - SelectAndScrollIntoView(this ListBox, int, bool focus)
    //   - SelectNextRow(this ListBox, int, bool loop, Func<object,bool>) → 向后查找
    //   - SelectPreviousRow(this ListBox, int, bool loop, Func<object,bool>) → 向前查找
    //   - FocusRow(this ListBox, int) → ContainerFromIndex + Focus
    //   - private SelectNextRow(ListBox, int, Direction, Func) → 遍历 Items
    //   - private SetKeyboardFocus(ListBox, int) → UpdateLayout + ContainerFromIndex + Keyboard.Focus + MainWindow.Instance.IsActive 检查
    //   - private ScrollRowIntoView(ListBox, int) → VisualTreeHelper.GetChild 遍历 ListBox→Border→ScrollViewer，手动 ScrollToVerticalOffset
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. ListBox → Avalonia.Controls.ListBox
    //   2. ListBoxItem → Avalonia.Controls.ListBoxItem
    //   3. VisualTreeHelper.GetChild 遍历找 ScrollViewer → Avalonia ListBox 内置 ScrollIntoView(item)
    //      spike 版 ScrollRowIntoView 直接调用 listBox.ScrollIntoView(Items[row])
    //   4. Keyboard.Focus → element.Focus()（Avalonia 11 实例方法）
    //   5. MainWindow.Instance.IsActive 检查跳过（WPF 工程专有，Avalonia spike 不检查窗口激活状态）
    //   6. listBox.UpdateLayout() 跳过（Avalonia 11 无等价的强制布局更新，ContainerFromIndex 直接可用）
    //   7. [Null] 特性移除（跨工程不可访问）
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

        public static bool SelectNextRow(this ListBox listBox, int row, bool loop, Func<object, bool> condition = null)
        {
            bool flag = listBox.SelectNextRow(row, Direction.Forward, condition);
            if (!flag && loop)
            {
                return listBox.SelectNextRow(-1, Direction.Forward, condition);
            }
            return flag;
        }

        public static bool SelectPreviousRow(this ListBox listBox, int row, bool loop, Func<object, bool> condition = null)
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

        private static bool SelectNextRow(this ListBox listBox, int row, Direction direction, Func<object, bool> condition)
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
            // spike: WPF 用 UpdateLayout + MainWindow.Instance.IsActive 检查，Avalonia 跳过
            if (listBox.ItemContainerGenerator.ContainerFromIndex(row) is ListBoxItem element)
            {
                element.Focus();
            }
        }

        private static void ScrollRowIntoView(ListBox listBox, int row)
        {
            // spike: WPF 用 VisualTreeHelper 遍历 ListBox→Border→ScrollViewer 手动滚动
            // Avalonia 11 ListBox 内置 ScrollIntoView(item)，直接调用
            if (row >= 0 && row < listBox.Items.Count)
            {
                listBox.ScrollIntoView(listBox.Items[row]);
            }
        }
    }
}
