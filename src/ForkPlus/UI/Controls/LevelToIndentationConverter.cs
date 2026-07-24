using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	public class LevelToIndentationConverter : IValueConverter
	{
		private static readonly double Offset = 10.0;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			double num = 0.0;
			if (value is int)
			{
				num = (double)((int)value - 1) * Offset;
			}
			return num;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
