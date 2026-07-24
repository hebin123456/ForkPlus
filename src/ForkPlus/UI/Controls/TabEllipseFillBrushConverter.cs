using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	public class TabEllipseFillBrushConverter : MarkupExtension, IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values.Length < 2)
			{
				return Brushes.Transparent;
			}
			SolidColorBrush solidColorBrush = (SolidColorBrush)values[0];
			if (!(bool)values[1])
			{
				return Brushes.Transparent;
			}
			return solidColorBrush ?? ClosableTabItem.IsDirtyDefaultBrush;
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
