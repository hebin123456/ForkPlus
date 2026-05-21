using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using ForkPlus.Accounts;

namespace ForkPlus.UI.UserControls
{
	public class PullRequestStateToColorConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is PullRequestState)
			{
				switch ((PullRequestState)value)
				{
				case PullRequestState.Open:
					return Theme.ApplicationColors.GreenBrush;
				case PullRequestState.Closed:
					return Theme.ApplicationColors.GrayBrush;
				case PullRequestState.Merged:
					return Theme.ApplicationColors.RedBrush;
				}
			}
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
