using System;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 CalendarDateRangUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/CalendarDateRangUserControl.xaml.cs（120 行）：
    //   - Start/End 公共属性 + Refresh() 同步 Calendar
    //   - MinDate/MaxDate 公共属性 + Refresh() 设置 DisplayDateStart/DisplayDateEnd
    //   - DateRangeChanged 事件（参数 EventArgs<CalendarDateRange>）
    //   - StartCalendar_SelectedDatesChanged / EndCalendar_SelectedDatesChanged：单选时
    //     若 Start > End 则强制 Start = End（反之亦然），然后触发 DateRangeChanged
    //   - Calendar_GotMouseCapture：CalendarDayButton/CalendarItem 释放鼠标捕获
    //     （spike 简化：CalendarDatePicker 无此问题，跳过）
    //   - Refresh()：用 _isCalendarUpdatingInProgress 标志避免回环
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF Calendar → CalendarDatePicker（事件参数 SelectionChangedEventArgs）
    //   - WPF Calendar.MinDate/MaxDate（不存在）→ CalendarDatePicker.DisplayDateStart/DisplayDateEnd
    //   - WPF Calendar.SelectedDate → CalendarDatePicker.SelectedDate
    //   - WPF Calendar.DisplayDate → CalendarDatePicker.DisplayDate
    //   - spike 简化：Start/End 初始值 = Today.AddDays(-30) / Today
    public partial class CalendarDateRangUserControl : UserControl
    {
        // ===== 内部类型（对照 WPF CalendarDateRange + EventArgs<T>）=====
        public class CalendarDateRange
        {
            public DateTime Start { get; }
            public DateTime End { get; }

            public CalendarDateRange(DateTime start, DateTime end)
            {
                Start = start;
                End = end;
            }
        }

        public class EventArgs<T> : EventArgs
        {
            public T Value { get; }

            public EventArgs(T value)
            {
                Value = value;
            }
        }

        // ===== 私有字段（对照 WPF）=====
        private DateTime _start = DateTime.Today.AddDays(-30);
        private DateTime _end = DateTime.Today;
        private bool _isCalendarUpdatingInProgress;

        // ===== 公共属性（对照 WPF）=====
        public DateTime Start
        {
            get => _start;
            set
            {
                _start = value;
                Refresh();
            }
        }

        public DateTime End
        {
            get => _end;
            set
            {
                _end = value;
                Refresh();
            }
        }

        public DateTime? MinDate { get; set; }

        public DateTime? MaxDate { get; set; }

        // ===== 事件（对照 WPF）=====
        // 对照 WPF: public event EventHandler<EventArgs<CalendarDateRange>> DateRangeChanged;
        public event EventHandler<EventArgs<CalendarDateRange>> DateRangeChanged;

        // spike 别名：与 WPF NotificationBarUserControl 中 RangeChanged 命名保持一致
        // （部分调用方用 RangeChanged 订阅）
        public event EventHandler<EventArgs<CalendarDateRange>> RangeChanged
        {
            add => DateRangeChanged += value;
            remove => DateRangeChanged -= value;
        }

        // ===== 构造函数（对照 WPF）=====
        public CalendarDateRangUserControl()
        {
            InitializeComponent();
            Refresh();
        }

        // ===== 事件处理（对照 WPF StartCalendar_SelectedDatesChanged / EndCalendar_SelectedDatesChanged）=====
        // 对照 WPF: private void StartCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        //   WPF: 取 StartCalendar.SelectedDate ?? Start + EndCalendar.SelectedDate ?? End，
        //         若 Start > End 则 Start = End，触发 DateRangeChanged
        //   Avalonia: 同样逻辑，CalendarDatePicker.SelectedDate 是 DateTime?
        private void StartCalendar_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isCalendarUpdatingInProgress) return;

            DateTime dateTime = StartCalendar.SelectedDate ?? Start;
            DateTime dateTime2 = EndCalendar.SelectedDate ?? End;
            if (dateTime > dateTime2)
            {
                dateTime = dateTime2;
                StartCalendar.SelectedDate = dateTime2;
            }
            DateRangeChanged?.Invoke(this, new EventArgs<CalendarDateRange>(new CalendarDateRange(dateTime, dateTime2)));
        }

        // 对照 WPF: private void EndCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        private void EndCalendar_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isCalendarUpdatingInProgress) return;

            DateTime dateTime = StartCalendar.SelectedDate ?? Start;
            DateTime dateTime2 = EndCalendar.SelectedDate ?? End;
            if (dateTime2 < dateTime)
            {
                dateTime2 = dateTime;
                EndCalendar.SelectedDate = dateTime;
            }
            DateRangeChanged?.Invoke(this, new EventArgs<CalendarDateRange>(new CalendarDateRange(dateTime, dateTime2)));
        }

        // ===== Refresh()（对照 WPF Refresh()）=====
        // 对照 WPF: private void Refresh()
        //   WPF: 用 DisplayDateStart/DisplayDateEnd 限制范围，SelectedDate + DisplayDate 同步
        //   Avalonia: 同样 API（CalendarDatePicker.DisplayDateStart/DisplayDateEnd/SelectedDate/DisplayDate）
        private void Refresh()
        {
            _isCalendarUpdatingInProgress = true;
            try
            {
                if (StartCalendar != null && EndCalendar != null)
                {
                    DateTime? minDate = MinDate;
                    if (minDate.HasValue)
                    {
                        DateTime valueOrDefault = minDate.GetValueOrDefault();
                        StartCalendar.DisplayDateStart = valueOrDefault;
                        EndCalendar.DisplayDateStart = valueOrDefault;
                    }
                    DateTime? maxDate = MaxDate;
                    if (maxDate.HasValue)
                    {
                        DateTime valueOrDefault2 = maxDate.GetValueOrDefault();
                        EndCalendar.DisplayDateEnd = valueOrDefault2;
                        StartCalendar.DisplayDateEnd = valueOrDefault2;
                    }
                    StartCalendar.SelectedDate = Start;
                    StartCalendar.DisplayDate = Start;
                    EndCalendar.SelectedDate = End;
                    EndCalendar.DisplayDate = End;
                }
            }
            finally
            {
                _isCalendarUpdatingInProgress = false;
            }
        }
    }
}
