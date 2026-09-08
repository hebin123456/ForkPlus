using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Dialogs
{
	public class GraphItemTypeToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is GraphItemType graphItemType && parameter is string text && Enum.TryParse<GraphItemType>(text, out GraphItemType expected))
			{
				return graphItemType == expected;
			}
			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
