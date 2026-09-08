using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
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
			return global::ForkPlus.UI.WpfCompat.WpfBinding.DoNothing;
		}
	}
}
