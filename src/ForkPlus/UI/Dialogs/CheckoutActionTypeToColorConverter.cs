using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Markup;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Dialogs
{
	public class CheckoutActionTypeToColorConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is CheckoutActionType)
			{
				switch ((CheckoutActionType)value)
				{
				case CheckoutActionType.None:
					return global::ForkPlus.UI.Theme.ApplicationColors.GreenBrush;
				case CheckoutActionType.Rebase:
					return global::ForkPlus.UI.Theme.ApplicationColors.YellowBrush;
				case CheckoutActionType.Merge:
					return global::ForkPlus.UI.Theme.ApplicationColors.YellowBrush;
				case CheckoutActionType.Reset:
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
