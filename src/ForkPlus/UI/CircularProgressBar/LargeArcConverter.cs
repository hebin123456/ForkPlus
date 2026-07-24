using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.CircularProgressBar
{
	public class LargeArcConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			double num = values[0].ExtractDouble();
			double num2 = values[1].ExtractDouble();
			double num3 = values[2].ExtractDouble();
			if (new double[3] { num, num2, num3 }.AnyNan())
			{
				// TODO(4.5-e): WPF Binding.DoNothing -> Avalonia BindingNotifications.DoNothing (skip target update).
				return BindingNotifications.DoNothing;
			}
			if (values.Length == 4)
			{
				double num4 = values[3].ExtractDouble();
				if (!double.IsNaN(num4) && num4 > 0.0)
				{
					num = (num3 - num2) * num4;
				}
			}
			return ((num3 <= num2) ? 1.0 : ((num - num2) / (num3 - num2))) > 0.5;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
