using System;
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	// 阶段 4.5：WPF System.Windows.Controls.CalendarDateRange → ForkPlus.Services.CalendarDateRange。
	// WPF Calendar 控件在 Avalonia 中为 Avalonia.Controls.Calendar，API 大体兼容。
	// WPF Calendar.GotMouseCapture + CalendarDayButton/CalendarItem 鼠标捕获释放
	// 在 Avalonia 中无对应；Avalonia Calendar 不持有鼠标捕获，移除该处理。
	// TODO(4.5-i): Avalonia Calendar 的 DisplayDateStart/DisplayDateEnd/DisplayDate 属性
	// 需运行时验证是否存在；如不存在需用 BlackoutDates 或自定义替代。
	public partial class CalendarDateRangUserControl : UserControl
	{
		private DateTime _start;

		private DateTime _end;

		private bool _isCalendarUpdatingInProgress;

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

		public event EventHandler<EventArgs<Services.CalendarDateRange>> DateRangeChanged;

		public CalendarDateRangUserControl()
		{
			InitializeComponent();
		}

		private void StartCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!_isCalendarUpdatingInProgress)
			{
				DateTime dateTime = StartCalendar.SelectedDate ?? Start;
				DateTime dateTime2 = EndCalendar.SelectedDate ?? End;
				if (dateTime > dateTime2)
				{
					dateTime = dateTime2;
					StartCalendar.SelectedDate = dateTime2;
				}
				this.DateRangeChanged?.Invoke(this, new EventArgs<Services.CalendarDateRange>(new Services.CalendarDateRange(dateTime, dateTime2)));
			}
		}

		private void EndCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!_isCalendarUpdatingInProgress)
			{
				DateTime dateTime = StartCalendar.SelectedDate ?? Start;
				DateTime dateTime2 = EndCalendar.SelectedDate ?? End;
				if (dateTime2 < dateTime)
				{
					dateTime2 = dateTime;
					EndCalendar.SelectedDate = dateTime;
				}
				this.DateRangeChanged?.Invoke(this, new EventArgs<Services.CalendarDateRange>(new Services.CalendarDateRange(dateTime, dateTime2)));
			}
		}

		private void Refresh()
		{
			_isCalendarUpdatingInProgress = true;
			DateTime? minDate = MinDate;
			if (minDate.HasValue)
			{
				DateTime valueOrDefault = minDate.GetValueOrDefault();
				// TODO(4.5-i): 验证 Avalonia Calendar.DisplayDateStart 是否存在。
				StartCalendar.DisplayDateStart = valueOrDefault;
				EndCalendar.DisplayDateStart = valueOrDefault;
			}
			minDate = MaxDate;
			if (minDate.HasValue)
			{
				DateTime valueOrDefault2 = minDate.GetValueOrDefault();
				// TODO(4.5-i): 验证 Avalonia Calendar.DisplayDateEnd 是否存在。
				EndCalendar.DisplayDateEnd = valueOrDefault2;
				StartCalendar.DisplayDateEnd = valueOrDefault2;
			}
			StartCalendar.SelectedDate = Start;
			StartCalendar.DisplayDate = Start;
			EndCalendar.SelectedDate = End;
			EndCalendar.DisplayDate = End;
			_isCalendarUpdatingInProgress = false;
		}
	}
}
