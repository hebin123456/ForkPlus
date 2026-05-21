using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using ForkPlus.Accounts;

namespace ForkPlus.UI.UserControls
{
	public class IssueStateToColorConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is IssueState issueState)
			{
				switch (issueState)
				{
				case IssueState.Open:
					return Theme.ApplicationColors.GreenBrush;
				case IssueState.Closed:
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
