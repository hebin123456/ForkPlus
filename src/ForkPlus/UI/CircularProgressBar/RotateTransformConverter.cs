using System;
using System.Globalization;
using System.Windows.Data;

namespace ForkPlus.UI.CircularProgressBar
{
	public class RotateTransformConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			double num = values[0].ExtractDouble();
			double num2 = values[1].ExtractDouble();
			double num3 = values[2].ExtractDouble();
			if (new double[3] { num, num2, num3 }.AnyNan())
			{
				return Binding.DoNothing;
			}
			double num4 = ((num3 <= num2) ? 1.0 : ((num - num2) / (num3 - num2)));
			return 360.0 * num4;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
