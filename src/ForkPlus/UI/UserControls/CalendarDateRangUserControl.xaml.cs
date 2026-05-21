using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;

namespace ForkPlus.UI.UserControls
{
	public partial class CalendarDateRangUserControl : UserControl
	{
		private DateTime _start;

		private DateTime _end;

		private bool _isCalendarUpdatingInProgress;

		public DateTime Start
		{
			get
			{
				return _start;
			}
			set
			{
				_start = value;
				Refresh();
			}
		}

		public DateTime End
		{
			get
			{
				return _end;
			}
			set
			{
				_end = value;
				Refresh();
			}
		}

		public DateTime? MinDate { get; set; }

		public DateTime? MaxDate { get; set; }

		public event EventHandler<EventArgs<CalendarDateRange>> DateRangeChanged;

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
				this.DateRangeChanged?.Invoke(this, new EventArgs<CalendarDateRange>(new CalendarDateRange(dateTime, dateTime2)));
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
				this.DateRangeChanged?.Invoke(this, new EventArgs<CalendarDateRange>(new CalendarDateRange(dateTime, dateTime2)));
			}
		}

		private void Calendar_GotMouseCapture(object sender, MouseEventArgs e)
		{
			UIElement uIElement = e.OriginalSource as UIElement;
			if (uIElement is CalendarDayButton || uIElement is CalendarItem)
			{
				uIElement.ReleaseMouseCapture();
			}
		}

		private void Refresh()
		{
			_isCalendarUpdatingInProgress = true;
			DateTime? minDate = MinDate;
			if (minDate.HasValue)
			{
				DateTime valueOrDefault = minDate.GetValueOrDefault();
				StartCalendar.DisplayDateStart = valueOrDefault;
				EndCalendar.DisplayDateStart = valueOrDefault;
			}
			minDate = MaxDate;
			if (minDate.HasValue)
			{
				DateTime valueOrDefault2 = minDate.GetValueOrDefault();
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
