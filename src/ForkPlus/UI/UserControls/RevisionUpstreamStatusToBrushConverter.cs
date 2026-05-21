using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.UserControls
{
	public class RevisionUpstreamStatusToBrushConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ((ActiveBranchCommitStatus)value != ActiveBranchCommitStatus.Ahead)
			{
				return Brushes.DarkGray;
			}
			return Theme.AccentBrush;
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
