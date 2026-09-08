using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Markup;
using Avalonia.Media;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	public class TabEllipseFillBrushConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IMultiValueConverter
	{
		public object Convert(global::System.Collections.Generic.IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			// Avalonia IMultiValueConverter 的 values 是 IList<object>，用 Count 而非 Length。
		if (values.Count < 2)
			{
				return Brushes.Transparent;
			}
			// Migration note：Avalonia MultiBinding 子绑定未解析时传 Avalonia.UnsetValueType（WPF 传 null），
		// 模板初始化期必然发生，强转 SolidColorBrush 抛 InvalidCastException；改用 as + 防御。
		SolidColorBrush solidColorBrush = values[0] as SolidColorBrush;
		bool flag;
		if (values[1] is bool b)
		{
			flag = b;
		}
		else
		{
			try { flag = global::System.Convert.ToBoolean(values[1], culture); }
			catch { flag = false; }
		}
		if (solidColorBrush != null)
		{
			return solidColorBrush;
		}
		return flag ? ClosableTabItem.IsDirtyDefaultBrush : Brushes.Transparent;
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
