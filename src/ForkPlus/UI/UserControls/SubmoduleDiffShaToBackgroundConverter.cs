using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	public class SubmoduleDiffShaToBackgroundConverter : MarkupExtension, IMultiValueConverter
	{
		// 阶段 5：IMultiValueConverter.Convert 签名为 IList<object?>，不再使用 object[]。
		public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
		{
			if (values.Count < 3)
			{
				return Brushes.Transparent;
			}
			Sha sha = (Sha)values[0];
			Sha sha2 = (Sha)values[1];
			Sha sha3 = (Sha)values[2];
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
