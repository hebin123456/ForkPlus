using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace ForkPlus.UI.Dialogs
{
	public class CheckoutActionTypeToColorConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is CheckoutActionType)
			{
				switch ((CheckoutActionType)value)
				{
				case CheckoutActionType.None:
					return Theme.ApplicationColors.GreenBrush;
				case CheckoutActionType.Rebase:
					return Theme.ApplicationColors.YellowBrush;
				case CheckoutActionType.Merge:
					return Theme.ApplicationColors.YellowBrush;
				case CheckoutActionType.Reset:
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
