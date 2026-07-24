using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
				// TODO(4.5-e): Avalonia's TryFindResource uses an out-parameter signature
				// (bool TryFindResource(object key, out object value)) unlike WPF's object-returning overload.
				Application.Current.TryFindResource("ForegroundBrush.Gray", out object grayBrush);
				return grayBrush as Brush;
			}
			// TODO(4.5-e): See above regarding Avalonia TryFindResource signature change.
			Application.Current.TryFindResource("ForegroundBrush", out object foregroundBrush);
			return foregroundBrush as Brush;
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
