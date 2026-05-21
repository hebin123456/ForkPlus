using System;

namespace ForkPlus.UI
{
	public static class ThemeTypeExtensions
	{
		public static Uri ResourceUri(this ThemeType themeType)
		{
			return themeType switch
			{
				ThemeType.Dark => new Uri("pack://application:,,,/ForkPlus;component/Theme/Generic.Dark.xaml"), 
				ThemeType.Light => new Uri("pack://application:,,,/ForkPlus;component/Theme/Generic.Light.xaml"), 
				_ => new Uri("pack://application:,,,/ForkPlus;component/Theme/Generic.Light.xaml"), 
			};
		}
	}
}

