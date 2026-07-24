using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	public class ResizeModeToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (ResizeMode)value == ResizeMode.CanResizeWithGrip;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
