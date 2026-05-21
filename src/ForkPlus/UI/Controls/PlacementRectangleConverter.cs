using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ForkPlus.UI.Controls
{
	public class PlacementRectangleConverter : IMultiValueConverter
	{
		public Thickness Margin { get; set; }

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values.Length == 2 && values[0] is double num && values[1] is double num2)
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
