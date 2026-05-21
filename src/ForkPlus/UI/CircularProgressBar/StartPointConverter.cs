using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

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
			return Binding.DoNothing;
		}
	}
}
