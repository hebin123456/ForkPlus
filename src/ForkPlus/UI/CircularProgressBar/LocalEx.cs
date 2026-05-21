using System.Collections.Generic;
using System.Linq;

namespace ForkPlus.UI.CircularProgressBar
{
	internal static class LocalEx
	{
		public static double ExtractDouble(this object val)
		{
			double valueOrDefault = (val as double?).GetValueOrDefault(double.NaN);
			if (!double.IsInfinity(valueOrDefault))
			{
				return valueOrDefault;
			}
			return double.NaN;
		}

		public static bool AnyNan(this IEnumerable<double> vals)
		{
			return vals.Any(double.IsNaN);
		}
	}
}
