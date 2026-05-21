using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	public class InteractiveRebaseActionToForegroundConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			InteractiveRebaseAction interactiveRebaseAction = (InteractiveRebaseAction)value;
			if (interactiveRebaseAction == InteractiveRebaseAction.Squash || interactiveRebaseAction == InteractiveRebaseAction.Fixup || interactiveRebaseAction == InteractiveRebaseAction.Drop)
			{
				return Application.Current.TryFindResource("ForegroundBrush.Gray") as Brush;
			}
			return Application.Current.TryFindResource("ForegroundBrush") as Brush;
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
