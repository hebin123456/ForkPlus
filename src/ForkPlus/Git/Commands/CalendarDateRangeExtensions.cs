using System;
using System.Windows.Controls;

namespace ForkPlus.Git.Commands
{
	public static class CalendarDateRangeExtensions
	{
		public static bool Contains(this CalendarDateRange dateRange, DateTime date)
		{
			if (date >= dateRange.Start)
			{
				return date < dateRange.End;
			}
			return false;
		}

		public static CalendarDateRange Quantize(this CalendarDateRange dateRange)
		{
			DateTime date = dateRange.Start.Date;
			DateTime date2 = dateRange.End.AddDays(1.0).Date;
			return new CalendarDateRange(date, date2);
		}
	}
}
