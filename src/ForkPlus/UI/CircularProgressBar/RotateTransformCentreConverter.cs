using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.CircularProgressBar
{
	public class RotateTransformCentreConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (double)value / 2.0;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// TODO(4.5-e): WPF Binding.DoNothing -> Avalonia BindingOperations.DoNothing (skip target update).
			return BindingOperations.DoNothing;
		}
	}
}
