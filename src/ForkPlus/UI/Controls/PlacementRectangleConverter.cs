using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	public class PlacementRectangleConverter : IMultiValueConverter
	{
		public Thickness Margin { get; set; }

		public object Convert(global::System.Collections.Generic.IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			// Avalonia IMultiValueConverter 的 values 是 IList<object>，用 Count 而非 Length。
			if (values.Count == 2 && values[0] is double num && values[1] is double num2)
			{
				Point point = new Point(Margin.Left, Margin.Top);
				Point point2 = new Point(num - Margin.Right, num2 - Margin.Bottom);
				return new Rect(point, point2);
			}
			// Migration note：Avalonia Rect 无 Empty 静态字段；绑定无值时返回 UnsetValue（保持目标属性默认值）。
			return global::Avalonia.AvaloniaProperty.UnsetValue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
