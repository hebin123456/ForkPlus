using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Git;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.UserControls
{
	public class RevisionUpstreamStatusToBrushConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ((ActiveBranchCommitStatus)value != ActiveBranchCommitStatus.Ahead)
			{
				return Brushes.DarkGray;
			}
			return global::ForkPlus.UI.Theme.AccentBrush;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
