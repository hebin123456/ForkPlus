using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	public class TabEllipseFillBrushConverter : MarkupExtension, IMultiValueConverter
	{
		// 阶段 5：IMultiValueConverter.Convert 签名为 IList<object?>，不再使用 object[]。
		// 阶段 6 修复：MultiBinding 在绑定源尚未解析时会传 Avalonia.UnsetValueType（占位符），
		// 直接强转 (SolidColorBrush) values[0] 会抛 InvalidCastException，中断整个 TabControl
		// 模板测量流程并触发窗口关闭（"一闪而过"根因之一）。需对每个值先做 UnsetValueType / null
		// 检查，任一未就绪时返回透明画刷，等待下一次绑定刷新。
		public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values.Count < 2)
			{
				return Brushes.Transparent;
			}
			// Avalonia.AvaloniaProperty.UnsetValue 为内部类型，用 typeof().Name 判断避免引用内部 API。
			object? brushValue = values[0];
			object? boolValue = values[1];
			if (brushValue == null || brushValue.GetType().Name == "UnsetValueType")
			{
				return Brushes.Transparent;
			}
			if (boolValue == null || boolValue.GetType().Name == "UnsetValueType" || !(boolValue is bool))
			{
				return Brushes.Transparent;
			}
			SolidColorBrush solidColorBrush = brushValue as SolidColorBrush;
			if (!(bool)boolValue)
			{
				return Brushes.Transparent;
			}
			return solidColorBrush ?? ClosableTabItem.IsDirtyDefaultBrush;
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
