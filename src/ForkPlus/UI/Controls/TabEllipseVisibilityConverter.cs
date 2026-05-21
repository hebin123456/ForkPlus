using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace ForkPlus.UI.Controls
{
	public class TabEllipseVisibilityConverter : MarkupExtension, IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values.Length < 2)
			{
				return Visibility.Collapsed;
			}
			SolidColorBrush solidColorBrush = (SolidColorBrush)values[0];
			return (!(bool)values[1]) ? ((solidColorBrush == null) ? Visibility.Collapsed : Visibility.Visible) : Visibility.Visible;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
