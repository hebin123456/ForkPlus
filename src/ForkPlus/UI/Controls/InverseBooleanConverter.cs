using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ForkPlus.UI.Controls
{
	[ValueConversion(typeof(bool), typeof(Visibility))]
	public class InverseBooleanConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return !(bool)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
