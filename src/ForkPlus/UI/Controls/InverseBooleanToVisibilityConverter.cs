using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF [ValueConversion] 特性在 Avalonia 无对应，Avalonia IValueConverter 无需声明目标类型。
	public class InverseBooleanToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return !(bool)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
