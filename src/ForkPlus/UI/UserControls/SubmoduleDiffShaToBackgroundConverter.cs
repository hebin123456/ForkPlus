using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	public class SubmoduleDiffShaToBackgroundConverter : MarkupExtension, IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values.Length < 3)
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
