using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Markup;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	public class TabEllipseVisibilityConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IMultiValueConverter
	{
		public object Convert(global::System.Collections.Generic.IList<object> values, Type targetType, object parameter, CultureInfo culture)
	{
		// Avalonia IMultiValueConverter 的 values 是 IList<object>，用 Count 而非 Length。
		if (values.Count < 2)
		{
			return false;
		}
		// Migration note：Avalonia MultiBinding 子绑定未解析时传 Avalonia.UnsetValueType（WPF 传 null），
		// 模板初始化期必然发生，强转 SolidColorBrush 抛 InvalidCastException。
		object brushVal = values[0];
		SolidColorBrush solidColorBrush = brushVal as SolidColorBrush;
		if (solidColorBrush == null)
		{
			// UnsetValue / DoNothing / null / 其他类型均按"无画刷"处理
		}
		bool flag;
		if (values[1] is bool b)
		{
			flag = b;
		}
		else if (values[1] is global::Avalonia.UnsetValueType || values[1] == null)
		{
			flag = false;
		}
		else
		{
			try { flag = global::System.Convert.ToBoolean(values[1], culture); }
			catch { flag = false; }
		}
		return (!flag) ? ((solidColorBrush == null) ? false : true) : true;
	}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
