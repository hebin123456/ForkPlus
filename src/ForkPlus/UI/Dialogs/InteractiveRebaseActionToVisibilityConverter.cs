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

namespace ForkPlus.UI.Dialogs
{
	public class InteractiveRebaseActionToVisibilityConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (!(value is InteractiveRebaseAction)) ? false : true;
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
