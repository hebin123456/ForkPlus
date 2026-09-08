using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Markup;
using ForkPlus.Git;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.UserControls
{
	public class RevisionUpstreamStatusToVisibilityConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return ((ActiveBranchCommitStatus)value == ActiveBranchCommitStatus.None) ? false : true;
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
