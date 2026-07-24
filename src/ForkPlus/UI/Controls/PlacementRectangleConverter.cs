using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	public class PlacementRectangleConverter : IMultiValueConverter
	{
		public Thickness Margin { get; set; }

		// 阶段 5：IMultiValueConverter.Convert 签名为 IList<object?>，不再使用 object[]。
		public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values.Count == 2 && values[0] is double num && values[1] is double num2)
			{
				Point point = new Point(Margin.Left, Margin.Top);
				Point point2 = new Point(num - Margin.Right, num2 - Margin.Bottom);
				return new Rect(point, point2);
			}
			return Rect.Empty;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
