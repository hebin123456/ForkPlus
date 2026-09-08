using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Markup;
using ForkPlus.Git;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Dialogs
{
	public class InteractiveRebaseActionToInteractiveRebaseComboBoxItemConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IValueConverter
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
			if (value is InteractiveRebaseComboBoxItem item)
			{
				return item.Action;
			}
			return BindingOperations.DoNothing;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
