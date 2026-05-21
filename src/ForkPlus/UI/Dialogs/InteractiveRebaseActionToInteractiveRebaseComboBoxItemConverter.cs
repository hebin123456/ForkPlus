using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Markup;
using ForkPlus.Git;

namespace ForkPlus.UI.Dialogs
{
	public class InteractiveRebaseActionToInteractiveRebaseComboBoxItemConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is InteractiveRebaseAction)
			{
				_ = (InteractiveRebaseAction)value;
				return InteractiveRebaseWindow.InteractiveRebaseComboBoxItems.FirstOrDefault((InteractiveRebaseComboBoxItem x) => x.Action == (InteractiveRebaseAction)value);
			}
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return ((InteractiveRebaseComboBoxItem)value).Action;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
