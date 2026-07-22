using System.Collections.Generic;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 NoUIAutomationListView（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/NoUIAutomationListView.cs（118 行）：
    //   - WPF NoUIAutomationListView : ListView
    //   - enum SelectOptions { None, ScrollIntoView, Focus }
    //   - class StubWindowAutomationPeer : FrameworkElementAutomationPeer
    //     （重写 OnCreateAutomationPeer 返回 Stub，屏蔽 UI 自动化树）
    //   - bool IsMultiselectionInProgress { get; set; }
    //   - double AvailableWidth => base.ActualWidth - 15.0 - 4.0 - 4.0
    //   - Select(int row, SelectOptions) / Select(IReadOnlyList<int> rows, SelectOptions)
    //     （多选 row 索引，按 ScrollIntoView / Focus 选项滚动并聚焦）
    //   - ScrollRowIntoView(ListBox, int row)（用 ScrollViewer.ScrollToVerticalOffset）
    //   - SetKeyboardFocus(ListBox, int row)（用 ItemContainerGenerator + Keyboard.Focus）
    //   - OnCreateAutomationPeer() → new StubWindowAutomationPeer(this)
    //   - UpdateResizableColumnWidth(int)（WPF GridView 列宽调整，依赖 base.View as GridView）
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 ListBox）：
    //   1. 基类 System.Windows.Controls.ListView → Avalonia.Controls.ListBox
    //      （task spec 明确要求：继承 ListBox；WPF ListView 在 Avalonia 11 中用 ListBox 替代）
    //   2. WPF AutomationPeer / OnCreateAutomationPeer → spike 跳过
    //      （Avalonia 11 自动化树由 AutomationProperties 控制，spike 不重写 peer）
    //   3. WPF VisualTreeHelper.GetChild → spike 跳过 ScrollRowIntoView
    //      （Avalonia 11 ListBox 内置 ScrollIntoView，spike 直接调用）
    //   4. WPF ItemContainerGenerator.ContainerFromIndex + Keyboard.Focus → spike 跳过
    //      （Avalonia 11 ListBox 用 ContainerFromIndex + Focus）
    //   5. WPF GridView 列宽调整 → spike 跳过（Avalonia 11 无 GridView，用 DataGrid 或 Grid）
    //   6. spike 保留 SelectOptions 枚举 + Select 方法签名（task spec 关键 API）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ListBox
    //   - SelectOptions 枚举
    //   - IsMultiselectionInProgress / AvailableWidth 属性
    //   - Select(int, SelectOptions) / Select(IReadOnlyList<int>, SelectOptions) 方法
    public class NoUIAutomationListView : ListBox
    {
        // 对照 WPF: public enum SelectOptions { None, ScrollIntoView, Focus }
        // 用 Flags 以支持 WPF 中 (SelectOptions)3 = ScrollIntoView | Focus 的位运算
        [System.Flags]
        public enum SelectOptions
        {
            None = 0,
            ScrollIntoView = 1,
            Focus = 2,
        }

        // 对照 WPF: public bool IsMultiselectionInProgress { get; set; }
        public bool IsMultiselectionInProgress { get; set; }

        // 对照 WPF: public double AvailableWidth => base.ActualWidth - 15.0 - 4.0 - 4.0;
        // spike 版：保留计算（spike 用 Bounds.Width 替代 WPF ActualWidth）
        public double AvailableWidth => Bounds.Width - 15.0 - 4.0 - 4.0;

        // 对照 WPF: public void Select(int row, SelectOptions options = (SelectOptions)3)
        public void Select(int row, SelectOptions options = (SelectOptions)3)
        {
            Select(new int[1] { row }, options);
        }

        // 对照 WPF: public void Select(IReadOnlyList<int> rows, SelectOptions options = (SelectOptions)3)
        public void Select(IReadOnlyList<int> rows, SelectOptions options = (SelectOptions)3)
        {
            if (rows.Count == 0)
            {
                return;
            }
            IsMultiselectionInProgress = true;
            SelectedItems.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i == rows.Count - 1)
                {
                    IsMultiselectionInProgress = false;
                }
                if (rows[i] >= 0 && rows[i] < Items.Count)
                {
                    SelectedItems.Add(Items[rows[i]]);
                }
            }
            if ((options & SelectOptions.ScrollIntoView) != 0 && rows[0] >= 0 && rows[0] < Items.Count)
            {
                // spike 版：用 Avalonia ListBox 内置 ScrollIntoView
                ScrollIntoView(Items[rows[0]]);
            }
            if ((options & SelectOptions.Focus) != 0)
            {
                // spike 版：spike 跳过 SetKeyboardFocus（依赖 MainWindow.Instance.IsActive）
                // 仅用 Avalonia 内置 Focus
                Focus();
            }
        }
    }
}
