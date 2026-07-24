using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	public class InteractiveRebaseActionToVisibilityConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value is InteractiveRebaseAction;
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
