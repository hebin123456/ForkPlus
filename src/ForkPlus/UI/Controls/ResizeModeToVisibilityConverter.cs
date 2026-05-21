using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ForkPlus.UI.Controls
{
	public class ResizeModeToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return ((ResizeMode)value != ResizeMode.CanResizeWithGrip) ? Visibility.Collapsed : Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
