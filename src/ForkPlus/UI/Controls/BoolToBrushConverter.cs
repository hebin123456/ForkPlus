using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 阶段 4.5：替代 WPF DataTrigger (bool -> Brush) 的转换器。
	/// 用法：Binding bool 属性，TrueBrush/FalseBrush 分别指定 true/false 时返回的画刷。
	/// </summary>
	public class BoolToBrushConverter : MarkupExtension, IValueConverter
	{
		public IBrush TrueBrush { get; set; }

		public IBrush FalseBrush { get; set; }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool flag = value is bool b && b;
			return flag ? TrueBrush : FalseBrush;
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
