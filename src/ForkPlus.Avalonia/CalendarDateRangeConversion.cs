using ServicesCalendarDateRange = ForkPlus.Services.CalendarDateRange;
using AvaloniaCalendarDateRange = ForkPlus.Avalonia.Controls.DateRangeButton.CalendarDateRange;

// Avalonia spike 版 CalendarDateRangeConversion（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/CalendarDateRangeConversion.cs（18 行）：
//   - WPF: public static class CalendarDateRangeConversion
//   - ToServiceCalendarDateRange(this System.Windows.Controls.CalendarDateRange) → Services.CalendarDateRange
//   - ToWpfCalendarDateRange(this Services.CalendarDateRange) → System.Windows.Controls.CalendarDateRange
//   - 双向转换：WPF UI 层 CalendarDateRange ↔ 业务层 Services.CalendarDateRange
//
// Avalonia 版差异（spike 简化策略）：
//   1. System.Windows.Controls.CalendarDateRange（WPF class）不存在 →
//      spike 用 Avalonia UI 层等价 POCO：Controls.DateRangeButton.CalendarDateRange
//   2. ToServiceCalendarDateRange 入参改为 AvaloniaCalendarDateRange → Services.CalendarDateRange
//   3. ToWpfCalendarDateRange → ToAvaloniaCalendarDateRange（反向：Services → Avalonia POCO）
//      （WPF 类型已不存在，反向方法改为产出 Avalonia POCO；无现有调用方，重命名安全）
//
// spike 简化（task spec 关键 API）：
//   - ToServiceCalendarDateRange(this AvaloniaCalendarDateRange) → Services.CalendarDateRange
//   - ToAvaloniaCalendarDateRange(this ServicesCalendarDateRange) → AvaloniaCalendarDateRange
namespace ForkPlus.Avalonia
{
	public static class CalendarDateRangeConversion
	{
		public static ServicesCalendarDateRange ToServiceCalendarDateRange(this AvaloniaCalendarDateRange range)
		{
			return range != null ? new ServicesCalendarDateRange(range.Start, range.End) : default;
		}

		public static AvaloniaCalendarDateRange ToAvaloniaCalendarDateRange(this ServicesCalendarDateRange range)
		{
			return new AvaloniaCalendarDateRange(range.Start, range.End);
		}
	}
}
