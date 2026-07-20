using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的日期范围结构体，替代 <see cref="System.Windows.Controls.CalendarDateRange"/>。
	/// 业务层使用此类型，UI 层在迁移时替换为对应的平台实现。
	/// </summary>
	public struct CalendarDateRange
	{
		public DateTime Start { get; set; }
		public DateTime End { get; set; }

		public CalendarDateRange(DateTime start, DateTime end)
		{
			Start = start;
			End = end;
		}
	}
}
