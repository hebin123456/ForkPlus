using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	public class TabEllipseVisibilityConverter : MarkupExtension, IMultiValueConverter
	{
		// 阶段 5：IMultiValueConverter.Convert 签名为 IList<object?>，不再使用 object[]。
		public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values.Count < 2)
			{
				return false;
			}
			SolidColorBrush solidColorBrush = (SolidColorBrush)values[0];
			return (!(bool)values[1]) ? ((solidColorBrush == null) ? false : true) : true;
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
