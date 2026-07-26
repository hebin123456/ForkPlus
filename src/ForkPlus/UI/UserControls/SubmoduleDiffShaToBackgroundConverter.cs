using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ForkPlus.Git;
using Theme = ForkPlus.UI.Theme;

namespace ForkPlus.UI.UserControls
{
	public class SubmoduleDiffShaToBackgroundConverter : MarkupExtension, IMultiValueConverter
	{
		// 阶段 5：IMultiValueConverter.Convert 签名为 IList<object?>，不再使用 object[]。
		// 阶段 6 修复：MultiBinding 在绑定源尚未解析时会传 Avalonia.UnsetValueType（占位符），
		// 直接强转 (Sha) values[i] 会抛 InvalidCastException。需先做 UnsetValueType / null 检查，
		// 任一未就绪时返回透明画刷，等待下一次绑定刷新。
		public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values.Count < 3)
			{
				return Brushes.Transparent;
			}
			object? v0 = values[0];
			object? v1 = values[1];
			object? v2 = values[2];
			if (v0 == null || v0.GetType().Name == "UnsetValueType" ||
				v1 == null || v1.GetType().Name == "UnsetValueType" ||
				v2 == null || v2.GetType().Name == "UnsetValueType")
			{
				return Brushes.Transparent;
			}
			if (!(v0 is Sha sha) || !(v1 is Sha sha2) || !(v2 is Sha sha3))
			{
				return Brushes.Transparent;
			}
			if (sha == sha3)
			{
				return Theme.Diff.AddedBrush;
			}
			if (sha == sha2)
			{
				return Theme.Diff.RemovedBrush;
			}
			return Brushes.Transparent;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
