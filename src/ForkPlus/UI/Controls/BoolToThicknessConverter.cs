using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 阶段 4.5：替代 WPF DataTrigger (bool -> Thickness) 的转换器。
	/// TrueThickness/FalseThickness 分别指定 true/false 时返回的 Thickness。
	/// 用于 RemoteBranchViewModel DataTemplate 中 HasDownstream -> Container.Margin 的迁移。
	/// </summary>
	public class BoolToThicknessConverter : MarkupExtension, IValueConverter
	{
		public Thickness TrueThickness { get; set; }

		public Thickness FalseThickness { get; set; }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool flag = value is bool b && b;
			return flag ? TrueThickness : FalseThickness;
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
