using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Git;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Dialogs
{
	public class InteractiveRebaseActionToForegroundConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IValueConverter
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
