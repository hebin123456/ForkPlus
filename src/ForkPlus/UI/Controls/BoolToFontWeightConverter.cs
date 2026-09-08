using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// Migration note：WPF DataTrigger 触发 FontWeight=Bold 无模板级等价物
	/// （IsActive 加粗当前分支徽章等场景）。本转换器把 bool 映射为
	/// Bold/Normal（false 时可用 ConverterParameter="Invert" 反转语义），
	/// 配合普通 Binding 替代 DataTrigger 的外观 Setter。
	/// </summary>
	public class BoolToFontWeightConverter : IValueConverter
	{
		public static readonly BoolToFontWeightConverter Instance = new BoolToFontWeightConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool flag = value is bool b && b;
			if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
			{
				flag = !flag;
			}
			return flag ? FontWeight.Bold : FontWeight.Normal;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is FontWeight weight)
			{
				return weight == FontWeight.Bold;
			}
			return false;
		}
	}
}
