using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 DateRangeButton（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/DateRangeButton.cs（93 行）：
    //   - WPF DateRangeButton : ToggleButton
    //   - CalendarDateRange _dateRange (DateTime Start/End)
    //   - DateRange property + DateRangeChanged event
    //   - MinDate / MaxDate properties
    //   - 构造函数：Checked += CreateCalendarPopup(this)
    //   - CreateCalendarPopup：Popup + AllowsTransparency + PopupAnimation.Fade +
    //     PlacementTarget + Binding IsOpen → IsChecked +
    //     CalendarDateRangUserControl（自定义 UserControl，未迁移）+
    //     MinDate/MaxDate/Start/End 设置 + VisualTreeAttachmentHelper.TrySetPopupChild
    //   - CalendarDateRangeUserControl_DateRangeChanged → DateRange = e.Value
    //   - UpdateTitle：Content = start.ToShortDateString() + " - " + end.ToShortDateString()
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 ToggleButton + 跳过 CalendarDateRangUserControl）：
    //   1. WPF ToggleButton 基类 → Avalonia.Controls.Primitives.ToggleButton
    //   2. WPF CalendarDateRange (System.Windows.Controls) → spike 本地 CalendarDateRange POCO
    //      （Avalonia 无对应类型，spike 定义 (DateTime Start, DateTime End) 简单 POCO）
    //   3. WPF Popup + CalendarDateRangUserControl → spike 跳过
    //      （CalendarDateRangUserControl 在 WPF 工程，未迁移；spike 不实现弹窗日历选择）
    //   4. WPF Binding IsOpen → IsChecked → spike 跳过（无 Popup）
    //   5. WPF VisualTreeAttachmentHelper.TrySetPopupChild → spike 跳过
    //   6. spike 保留 DateRange / MinDate / MaxDate properties + DateRangeChanged event
    //   7. spike 保留 UpdateTitle（Content = "start - end"）
    //   8. spike 新增 SetDateRange(DateTime, DateTime) 公共方法
    //      （替代 WPF 弹窗选择，调用方直接设置）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 ToggleButton
    //   - CalendarDateRange POCO (Start/End DateTime)
    //   - DateRange / MinDate / MaxDate properties
    //   - DateRangeChanged event
    //   - UpdateTitle（Content = "start - end"）
    //   - SetDateRange 公共方法（spike 替代弹窗）
    public class DateRangeButton : ToggleButton
    {
        // spike 本地 POCO：替代 WPF System.Windows.Controls.CalendarDateRange
        // 对照 WPF: CalendarDateRange (DateTime Start, DateTime End)
        public class CalendarDateRange
        {
            // 对照 WPF: public DateTime Start { get; }
            public DateTime Start { get; set; }

            // 对照 WPF: public DateTime End { get; }
            public DateTime End { get; set; }

            public CalendarDateRange() { }

            public CalendarDateRange(DateTime start, DateTime end)
            {
                Start = start;
                End = end;
            }
        }

        // 对照 WPF: private CalendarDateRange _dateRange = new CalendarDateRange(DateTime.Now, DateTime.Now)
        private CalendarDateRange _dateRange = new CalendarDateRange(DateTime.Now, DateTime.Now);

        // 对照 WPF: public CalendarDateRange DateRange
        //   set: _dateRange = value; UpdateTitle(); DateRangeChanged?.Invoke(this, EventArgs.Empty);
        public CalendarDateRange DateRange
        {
            get => _dateRange;
            set
            {
                _dateRange = value;
                UpdateTitle();
                DateRangeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // 对照 WPF: public DateTime? MinDate { get; set; }
        public DateTime? MinDate { get; set; }

        // 对照 WPF: public DateTime? MaxDate { get; set; }
        public DateTime? MaxDate { get; set; }

        // 对照 WPF: public event EventHandler DateRangeChanged
        public event EventHandler DateRangeChanged;

        public DateRangeButton()
        {
            // 对照 WPF: base.Checked += delegate { CreateCalendarPopup(this); };
            // spike 跳过：不实现弹窗日历选择
            // 调用方通过 SetDateRange 显式设置 DateRange
            UpdateTitle();
        }

        // spike 新增：替代 WPF CreateCalendarPopup + CalendarDateRangeUserControl_DateRangeChanged
        // 调用方完成日历选择后通过此方法注入 DateRange
        public void SetDateRange(DateTime start, DateTime end)
        {
            DateRange = new CalendarDateRange(start, end);
        }

        // 对照 WPF: private void UpdateTitle()
        //   base.Content = start.ToShortDateString() + " - " + end.ToShortDateString();
        private void UpdateTitle()
        {
            DateTime start = _dateRange.Start;
            DateTime end = _dateRange.End;
            Content = start.ToShortDateString() + " - " + end.ToShortDateString();
        }
    }
}
