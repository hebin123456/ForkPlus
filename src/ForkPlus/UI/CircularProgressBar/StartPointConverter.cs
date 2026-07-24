using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.CircularProgressBar
{
	public class StartPointConverter : IValueConverter
	{
		[Obsolete]
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is double && (double)value > 0.0)
			{
				return new Point((double)value / 2.0, 0.0);
			}
			return default(Point);
		}

		[Obsolete]
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// TODO(4.5-e): WPF Binding.DoNothing -> Avalonia BindingNotifications.DoNothing (skip target update).
			return BindingNotifications.DoNothing;
		}
	}
}
