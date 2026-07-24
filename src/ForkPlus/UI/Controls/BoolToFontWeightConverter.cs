using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 阶段 4.5：替代 WPF DataTrigger (bool=true -> FontWeight) 的转换器。
	/// TrueWeight 指定 true 时返回的 FontWeight；false 时返回 Normal（默认）。
	/// </summary>
	public class BoolToFontWeightConverter : MarkupExtension, IValueConverter
	{
		public FontWeight TrueWeight { get; set; } = FontWeight.Normal;

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool flag = value is bool b && b;
			return flag ? TrueWeight : FontWeight.Normal;
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
