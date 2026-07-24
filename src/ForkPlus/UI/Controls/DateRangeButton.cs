using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF System.Windows.Controls.CalendarDateRange
	// → ForkPlus.Services.CalendarDateRange（平台无关结构体）。
	// DateRangeButton.DateRange 类型直接改为 Services.CalendarDateRange，
	// 调用方不再需要 ToServiceCalendarDateRange() 转换。
	// CalendarDateRangeBridge / CalendarDateRangeConversion 两个桥接文件已可删除。
	public class DateRangeButton : ToggleButton
	{
		// 阶段 4.5：直接使用 Services.CalendarDateRange，消除 WPF 依赖。
		private Services.CalendarDateRange _dateRange = new Services.CalendarDateRange(DateTime.Now, DateTime.Now);

		public Services.CalendarDateRange DateRange
		{
			get => _dateRange;
			set
			{
				_dateRange = value;
				UpdateTitle();
				this.DateRangeChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public DateTime? MinDate { get; set; }

		public DateTime? MaxDate { get; set; }

		public event EventHandler DateRangeChanged;

		public DateRangeButton()
		{
			base.Checked += delegate
			{
				CreateCalendarPopup(this);
			};
		}

		private void CreateCalendarPopup(ToggleButton parentButton)
		{
			Popup popup = new Popup();
			popup.HorizontalOffset = -100.0;
			popup.VerticalOffset = 0.0;
			// 阶段 4.5：WPF StaysOpen=false → Avalonia IsLightDismissEnabled=true（点击外部关闭）。
			popup.IsLightDismissEnabled = true;
			// 阶段 4.5：WPF AllowsTransparency / PopupAnimation.Fade 在 Avalonia 中无对应；Popup 默认透明。
			popup.PlacementTarget = this;
			popup.Opened += delegate
			{
				parentButton.Disable();
			};
			popup.Closed += delegate
			{
				// 阶段 4.5：WPF BindingOperations.ClearBinding → 直接取消 IsChecked。
				// Avalonia Popup 通过 IsLightDismissEnabled 自动关闭，无需双向绑定到 IsChecked。
				parentButton.IsChecked = false;
				parentButton.Enable();
			};
			// 阶段 4.5：WPF BindingOperations.SetBinding(IsOpen ↔ IsChecked)
			// → Avalonia 直接设置 IsOpen=true（依赖 IsLightDismissEnabled 处理关闭）。
			popup.IsOpen = true;
			CalendarDateRangUserControl calendarDateRangUserControl = new CalendarDateRangUserControl();
			calendarDateRangUserControl.DateRangeChanged += CalendarDateRangeUserControl_DateRangeChanged;
			DateTime? minDate = MinDate;
			if (minDate.HasValue)
			{
				DateTime valueOrDefault = minDate.GetValueOrDefault();
				calendarDateRangUserControl.MinDate = valueOrDefault;
			}
			minDate = MaxDate;
			if (minDate.HasValue)
			{
				DateTime valueOrDefault2 = minDate.GetValueOrDefault();
				calendarDateRangUserControl.MaxDate = valueOrDefault2;
			}
			calendarDateRangUserControl.Start = DateRange.Start;
			calendarDateRangUserControl.End = DateRange.End;
			VisualTreeAttachmentHelper.TrySetPopupChild(popup, calendarDateRangUserControl, GetType().Name + ".Popup");
		}

		private void CalendarDateRangeUserControl_DateRangeChanged(object sender, EventArgs<Services.CalendarDateRange> e)
		{
			DateRange = e.Value;
		}

		private void UpdateTitle()
		{
			DateTime start = DateRange.Start;
			DateTime end = DateRange.End;
			base.Content = start.ToShortDateString() + " - " + end.ToShortDateString();
		}
	}
}
