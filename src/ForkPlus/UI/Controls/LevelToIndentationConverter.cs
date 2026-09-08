using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	public class LevelToIndentationConverter : IValueConverter
	{
		private static readonly double IndentStep = 10.0;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is not int level)
			{
				return new GridLength(0.0);
			}
			int indentLevel = Math.Max(0, level - 1);
			return new GridLength(indentLevel * IndentStep);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
