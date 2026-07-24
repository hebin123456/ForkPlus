using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

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

	/// <summary>
	/// 阶段 4.5：替代 WPF DataTrigger (enum == value -> TextDecorations) 的转换器。
	/// ConverterParameter 可为枚举值或字符串（字符串会按绑定值的枚举类型解析）；
	/// 绑定值等于参数时返回 MatchDecorations（默认 Strikethrough），否则返回 UnsetValue（无装饰）。
	/// 用于 InteractiveRebaseWindow 中 Action=Drop -> TextDecorations=Strikethrough 的迁移。
	/// </summary>
	public class EnumToTextDecorationsConverter : MarkupExtension, IValueConverter
	{
		// 阶段 5：Avalonia 11 中 TextDecorations 是静态类，不能作为属性类型。
		// 改用实例类型 TextDecorationCollection（Avalonia.Media）。
		public TextDecorationCollection MatchDecorations { get; set; } = TextDecorations.Strikethrough;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null || parameter == null)
			{
				return AvaloniaProperty.UnsetValue;
			}
			object compareValue = parameter;
			if (parameter is string s && value.GetType().IsEnum)
			{
				try
				{
					compareValue = Enum.Parse(value.GetType(), s, ignoreCase: true);
				}
				catch
				{
					return AvaloniaProperty.UnsetValue;
				}
			}
			return value.Equals(compareValue) ? MatchDecorations : AvaloniaProperty.UnsetValue;
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
