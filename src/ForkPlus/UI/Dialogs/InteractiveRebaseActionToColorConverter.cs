using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	public class InteractiveRebaseActionToColorConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is InteractiveRebaseAction)
			{
				switch ((InteractiveRebaseAction)value)
				{
				case InteractiveRebaseAction.Pick:
					return Theme.ApplicationColors.GreenBrush;
				case InteractiveRebaseAction.Edit:
					return Theme.ApplicationColors.YellowBrush;
				case InteractiveRebaseAction.Reword:
					return Theme.ApplicationColors.YellowBrush;
				case InteractiveRebaseAction.Squash:
					return Theme.ApplicationColors.GrayBrush;
				case InteractiveRebaseAction.Fixup:
					return Theme.ApplicationColors.GrayBrush;
				case InteractiveRebaseAction.Drop:
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
