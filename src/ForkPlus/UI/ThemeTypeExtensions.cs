using System;
using System.Collections.Generic;

namespace ForkPlus.UI
{
	public static class ThemeTypeExtensions
	{
		/// <summary>皮肤名（用于 Generic.{SkinName}.xaml 文件名 + i18n key）。</summary>
		public static string SkinName(this ThemeType themeType)
		{
			switch (themeType)
			{
				case ThemeType.Light: return "Light";
				case ThemeType.Dark: return "Dark";
				case ThemeType.SolarizedLight: return "SolarizedLight";
				case ThemeType.SolarizedDark: return "SolarizedDark";
				case ThemeType.Dracula: return "Dracula";
				case ThemeType.GitHubLight: return "GitHubLight";
				case ThemeType.GitHubDark: return "GitHubDark";
				case ThemeType.Monokai: return "Monokai";
				case ThemeType.Purple: return "Purple";
				default: return "Light";
			}
		}

		/// <summary>皮肤的基底明暗。用于 WebView2 PreferredColorScheme、系统跟随主题、
		/// UserColorBrushes 等只能二选一的场景。基底为 Dark 的皮肤用 Dark 资源兜底。</summary>
		public static bool IsDarkBase(this ThemeType themeType)
		{
			switch (themeType)
			{
				case ThemeType.Dark:
				case ThemeType.SolarizedDark:
				case ThemeType.Dracula:
				case ThemeType.GitHubDark:
				case ThemeType.Monokai:
				case ThemeType.Purple:
					return true;
				default:
					return false;
			}
		}

		/// <summary>皮肤对应的资源字典 URI（Generic.{SkinName}.xaml）。</summary>
		public static Uri ResourceUri(this ThemeType themeType)
		{
			return new Uri("pack://application:,,,/ForkPlus;component/Theme/Generic." + themeType.SkinName() + ".xaml");
		}

		/// <summary>所有内置预设皮肤，用于主题选择菜单遍历。</summary>
		public static readonly IReadOnlyList<ThemeType> AllThemes = new ThemeType[]
		{
			ThemeType.Light,
			ThemeType.Dark,
			ThemeType.SolarizedLight,
			ThemeType.SolarizedDark,
			ThemeType.GitHubLight,
			ThemeType.GitHubDark,
			ThemeType.Dracula,
			ThemeType.Monokai,
			ThemeType.Purple
		};
	}
}
