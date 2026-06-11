using System;
using WpfCalendarDateRange = System.Windows.Controls.CalendarDateRange;
using ServicesCalendarDateRange = ForkPlus.Services.CalendarDateRange;

namespace ForkPlus.UI
{
	public static class CalendarDateRangeConversion
	{
		public static ServicesCalendarDateRange ToServiceCalendarDateRange(this WpfCalendarDateRange range)
		{
			return new ServicesCalendarDateRange(range.Start, range.End);
		}

		public static WpfCalendarDateRange ToWpfCalendarDateRange(this ServicesCalendarDateRange range)
		{
			return new WpfCalendarDateRange(range.Start, range.End);
		}
	}
}
