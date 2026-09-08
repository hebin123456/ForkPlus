using ForkPlus.UI.Helpers;
using System;
using WpfCalendarDateRange = global::Avalonia.Controls.CalendarDateRange;
using ServicesCalendarDateRange = ForkPlus.Services.CalendarDateRange;

namespace ForkPlus.UI
{
	/// <summary>
	/// WPF CalendarDateRange �?Services.CalendarDateRange 转换桥接�?	/// 迁移�?Avalonia 时，UI 层不再使�?System.Windows.Controls.CalendarDateRange，此文件可删除�?	/// </summary>
	public static class CalendarDateRangeBridge
	{
		public static ServicesCalendarDateRange ToServices(this WpfCalendarDateRange range)
		{
			return new ServicesCalendarDateRange(range.Start, range.End);
		}

		public static ServicesCalendarDateRange? ToServicesNullable(this WpfCalendarDateRange range)
		{
			return range != null ? new ServicesCalendarDateRange(range.Start, range.End) : (ServicesCalendarDateRange?)null;
		}
	}
}
