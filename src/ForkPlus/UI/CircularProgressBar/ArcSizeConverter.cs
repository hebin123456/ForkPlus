// 阶段 4.5：WPF Binding.DoNothing → Avalonia AvaloniaProperty.UnsetValue（信号绑定不更新目标，使用 fallback value）。
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.CircularProgressBar
{
	public class ArcSizeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is double && (double)value > 0.0)
			{
				return new Size((double)value / 2.0, (double)value / 2.0);
			}
			return default(Point);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return AvaloniaProperty.UnsetValue;
		}
	}
}
