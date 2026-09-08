using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Controls
{
	public class DateRangeButton : ToggleButton
	{
		private CalendarDateRange _dateRange = new CalendarDateRange(DateTime.Now, DateTime.Now);

		public CalendarDateRange DateRange
		{
			get
			{
				return _dateRange;
			}
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
			global::ForkPlus.UI.WpfCompat.Events.AddChecked(this, delegate // Migration note：base 不能作为独立参数
			{
				CreateCalendarPopup(this);
			});
		}

		private void CreateCalendarPopup(ToggleButton parentButton)
		{
			Popup popup = new Popup();
			popup.HorizontalOffset = -100.0;
			popup.VerticalOffset = 0.0;
			popup.IsLightDismissEnabled= (!false);
			/* Migration note: AllowsTransparency 已删除 */;
			/* Migration note: PopupAnimation 已删除 */;
			popup.PlacementTarget = this;
			popup.Opened += delegate
			{
				parentButton.Disable();
			};
			popup.Closed += delegate
			{
				global::ForkPlus.UI.WpfCompat.BindingCompat.ClearBinding(popup, Popup.IsOpenProperty);
				parentButton.Enable();
			};
			global::ForkPlus.UI.WpfCompat.BindingCompat.SetBinding(popup, Popup.IsOpenProperty, new Binding("IsChecked")
			{
				Source = parentButton
			});
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

		private void CalendarDateRangeUserControl_DateRangeChanged(object sender, EventArgs<CalendarDateRange> e)
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
