using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 阶段 4.5：替代 WPF DataTrigger (enum == value) 的转换器。
	/// ConverterParameter 为目标枚举值，返回 bool 表示绑定值是否等于该参数。
	/// </summary>
	public class EnumEqualsConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || parameter == null)
			{
				return value == parameter;
			}
			return value.Equals(parameter);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}

	/// <summary>
	/// 阶段 4.5：EnumEqualsConverter 的反义版本，返回绑定值是否不等于 ConverterParameter。
	/// </summary>
	public class EnumNotEqualsConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || parameter == null)
			{
				return value != parameter;
			}
			return !value.Equals(parameter);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
