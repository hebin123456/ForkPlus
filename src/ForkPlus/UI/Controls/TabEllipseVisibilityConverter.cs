using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	public class TabEllipseVisibilityConverter : MarkupExtension, IMultiValueConverter
	{
		// 阶段 5：IMultiValueConverter.Convert 签名为 IList<object?>，不再使用 object[]。
		// 阶段 6 修复：MultiBinding 在绑定源尚未解析时会传 Avalonia.UnsetValueType（占位符），
		// 直接强转 (SolidColorBrush) 会抛 InvalidCastException 中断整个模板测量流程。
		// 需对每个值先做 UnsetValueType / null 检查，任一未就绪时返回 false（隐藏 ellipse）。
		public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values.Count < 2)
			{
				return false;
			}
			// Avalonia.AvaloniaProperty.UnsetValue 为内部类型，用 typeof().Name 判断避免引用内部 API。
			object? brushValue = values[0];
			object? boolValue = values[1];
			if (brushValue == null || brushValue.GetType().Name == "UnsetValueType")
			{
				return false;
			}
			if (boolValue == null || boolValue.GetType().Name == "UnsetValueType" || !(boolValue is bool))
			{
				return false;
			}
			SolidColorBrush solidColorBrush = brushValue as SolidColorBrush;
			return (!(bool)boolValue) ? (solidColorBrush != null) : true;
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
