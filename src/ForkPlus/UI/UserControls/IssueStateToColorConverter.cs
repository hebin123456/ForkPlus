using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Markup;
using ForkPlus.Accounts;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.UserControls
{
	public class IssueStateToColorConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is IssueState issueState)
			{
				switch (issueState)
				{
				case IssueState.Open:
					return global::ForkPlus.UI.Theme.ApplicationColors.GreenBrush;
				case IssueState.Closed:
					return global::ForkPlus.UI.Theme.ApplicationColors.RedBrush;
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
