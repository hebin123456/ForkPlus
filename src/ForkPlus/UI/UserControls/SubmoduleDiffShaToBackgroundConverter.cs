using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Git;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.UserControls
{
	public class SubmoduleDiffShaToBackgroundConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IMultiValueConverter
	{
		public object Convert(global::System.Collections.Generic.IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values.Count < 3) // Migration note：WPF 数组 → Avalonia IList<object>。
			{
				return Brushes.Transparent;
			}
			Sha sha = (Sha)values[0];
			Sha sha2 = (Sha)values[1];
			Sha sha3 = (Sha)values[2];
			if (sha == sha3)
			{
				return global::ForkPlus.UI.Theme.Diff.AddedBrush;
			}
			if (sha == sha2)
			{
				return global::ForkPlus.UI.Theme.Diff.RemovedBrush;
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
