using System;
using System.Globalization;
using System.Windows.Data;

namespace ForkPlus.UI.CircularProgressBar
{
	public class RotateTransformCentreConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (double)value / 2.0;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}
}
