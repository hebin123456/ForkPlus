using ServicesCalendarDateRange = ForkPlus.Services.CalendarDateRange;
using AvaloniaCalendarDateRange = ForkPlus.Avalonia.Controls.DateRangeButton.CalendarDateRange;

// Avalonia spike 版 CalendarDateRangeBridge（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/CalendarDateRangeBridge.cs（21 行）：
//   - WPF: public static class CalendarDateRangeBridge
//   - ToServices(this System.Windows.Controls.CalendarDateRange) → Services.CalendarDateRange
//   - ToServicesNullable(this System.Windows.Controls.CalendarDateRange) → Services.CalendarDateRange?
//   - 桥接 WPF UI 层 CalendarDateRange（class）到业务层 Services.CalendarDateRange（struct）
//   - WPF 源注释：迁移到 Avalonia 时 UI 层不再用 System.Windows.Controls.CalendarDateRange，此文件可删除
//
// Avalonia 版差异（spike 简化策略）：
//   1. System.Windows.Controls.CalendarDateRange（WPF class）不存在 →
//      spike 用 Avalonia UI 层等价 POCO：Controls.DateRangeButton.CalendarDateRange
//      （本 spike 用 (DateTime Start, DateTime End) POCO 替代 WPF CalendarDateRange）
//   2. ToServices / ToServicesNullable 改为从 AvaloniaCalendarDateRange 桥接到 Services.CalendarDateRange
//   3. Services.CalendarDateRange 为 struct（不可空），ToServices 对 null 入参返回 default
//
// spike 简化（task spec 关键 API）：
//   - ToServices(this AvaloniaCalendarDateRange) → Services.CalendarDateRange
//   - ToServicesNullable(this AvaloniaCalendarDateRange) → Services.CalendarDateRange?
namespace ForkPlus.Avalonia
{
	/// <summary>Avalonia UI 层 CalendarDateRange（DateRangeButton POCO）到
	/// Services.CalendarDateRange 的转换桥接。替代 WPF 的 System.Windows.Controls.CalendarDateRange 桥接。</summary>
	public static class CalendarDateRangeBridge
	{
		public static ServicesCalendarDateRange ToServices(this AvaloniaCalendarDateRange range)
		{
			return range != null ? new ServicesCalendarDateRange(range.Start, range.End) : default;
		}

		public static ServicesCalendarDateRange? ToServicesNullable(this AvaloniaCalendarDateRange range)
		{
			return range != null ? new ServicesCalendarDateRange(range.Start, range.End) : (ServicesCalendarDateRange?)null;
		}
	}
}
