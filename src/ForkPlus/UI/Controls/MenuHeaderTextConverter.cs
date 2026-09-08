using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	public class MenuHeaderTextConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is not string text)
			{
				return value;
			}

			return StripAccessKeyMarkers(text);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		private static string StripAccessKeyMarkers(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			var result = new System.Text.StringBuilder(text.Length);
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '_')
				{
					if (i + 1 < text.Length && text[i + 1] == '_')
					{
						result.Append('_');
						i++;
					}
					continue;
				}
				result.Append(text[i]);
			}
			return result.ToString();
		}
	}
}
