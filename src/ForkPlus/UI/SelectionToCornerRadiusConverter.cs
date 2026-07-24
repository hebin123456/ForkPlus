using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace ForkPlus.UI
{
	public class SelectionToCornerRadiusConverter : MarkupExtension, IValueConverter
	{
		private static CornerRadius CornerRadiusNone = new CornerRadius(0.0, 0.0, 0.0, 0.0);

		private static CornerRadius CornerRadiusAll = new CornerRadius(4.0, 4.0, 4.0, 4.0);

		private static CornerRadius CornerRadiusTop = new CornerRadius(4.0, 4.0, 0.0, 0.0);

		private static CornerRadius CornerRadiusBottom = new CornerRadius(0.0, 0.0, 4.0, 4.0);

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is ListBoxSelectionType))
			{
				return CornerRadiusAll;
			}
			return (ListBoxSelectionType)value switch
			{
				ListBoxSelectionType.None => CornerRadiusNone, 
				ListBoxSelectionType.Top => CornerRadiusTop, 
				ListBoxSelectionType.Middle => CornerRadiusNone, 
				ListBoxSelectionType.Bottom => CornerRadiusBottom, 
				_ => CornerRadiusAll, 
			};
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
