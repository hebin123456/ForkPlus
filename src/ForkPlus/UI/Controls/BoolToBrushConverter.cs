using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 阶段 4.5：替代 WPF DataTrigger (bool -> Brush) 的转换器。
	/// TrueBrush/FalseBrush 分别指定 true/false 时返回的画刷；
	/// 若 TrueBrush 为 null（默认场景，如 IsReachable=True 时保持继承 Foreground），返回 UnsetValue。
	/// </summary>
	/// <remarks>
	/// 阶段 5：TrueBrush/FalseBrush 改为 StyledProperty，以支持 XAML 中
	/// &lt;controls:BoolToBrushConverter TrueBrush="{DynamicResource XXX}" /&gt; 的绑定语法。
	/// 注意：作为 MarkupExtension 时，AvaloniaObject 仍可作为 IValueConverter 使用。
	/// </remarks>
	public class BoolToBrushConverter : AvaloniaObject, IValueConverter
	{
		public static readonly StyledProperty<IBrush> TrueBrushProperty =
			AvaloniaProperty.Register<BoolToBrushConverter, IBrush>(nameof(TrueBrush));

		public static readonly StyledProperty<IBrush> FalseBrushProperty =
			AvaloniaProperty.Register<BoolToBrushConverter, IBrush>(nameof(FalseBrush));

		public IBrush TrueBrush
		{
			get => GetValue(TrueBrushProperty);
			set => SetValue(TrueBrushProperty, value);
		}

		public IBrush FalseBrush
		{
			get => GetValue(FalseBrushProperty);
			set => SetValue(FalseBrushProperty, value);
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool flag = value is bool b && b;
			IBrush result = flag ? TrueBrush : FalseBrush;
			return result ?? AvaloniaProperty.UnsetValue;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}
