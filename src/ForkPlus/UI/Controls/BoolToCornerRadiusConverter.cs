using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 阶段 4.5：替代 WPF DataTrigger (bool -> CornerRadius) 的转换器。
	/// TrueCornerRadius/FalseCornerRadius 分别指定 true/false 时返回的 CornerRadius。
	/// 用于 RemoteBranchViewModel DataTemplate 中 HasDownstream -> Border.CornerRadius 的迁移。
	/// </summary>
	public class BoolToCornerRadiusConverter : MarkupExtension, IValueConverter
	{
		public CornerRadius TrueCornerRadius { get; set; }

		public CornerRadius FalseCornerRadius { get; set; }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool flag = value is bool b && b;
			return flag ? TrueCornerRadius : FalseCornerRadius;
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
