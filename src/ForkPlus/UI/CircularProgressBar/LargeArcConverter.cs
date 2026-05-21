using System;
using System.Globalization;
using System.Windows.Data;

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
				return Binding.DoNothing;
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
