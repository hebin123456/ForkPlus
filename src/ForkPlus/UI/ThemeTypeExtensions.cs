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
				case ThemeType.PurpleLight: return "PurpleLight";
				case ThemeType.PurpleDark: return "PurpleDark";
				case ThemeType.GreenLight: return "GreenLight";
				case ThemeType.GreenDark: return "GreenDark";
				// v3.1.1：纯色主题
				case ThemeType.RedLight: return "RedLight";
				case ThemeType.RedDark: return "RedDark";
				case ThemeType.OrangeLight: return "OrangeLight";
				case ThemeType.OrangeDark: return "OrangeDark";
				case ThemeType.YellowLight: return "YellowLight";
				case ThemeType.YellowDark: return "YellowDark";
				case ThemeType.CyanLight: return "CyanLight";
				case ThemeType.CyanDark: return "CyanDark";
				case ThemeType.BlueLight: return "BlueLight";
				case ThemeType.BlueDark: return "BlueDark";
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
				case ThemeType.PurpleDark:
				case ThemeType.GreenDark:
				case ThemeType.RedDark:
				case ThemeType.OrangeDark:
				case ThemeType.YellowDark:
				case ThemeType.CyanDark:
				case ThemeType.BlueDark:
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
			ThemeType.PurpleLight,
			ThemeType.PurpleDark,
			ThemeType.GreenLight,
			ThemeType.GreenDark,
			ThemeType.RedLight,
			ThemeType.RedDark,
			ThemeType.OrangeLight,
			ThemeType.OrangeDark,
			ThemeType.YellowLight,
			ThemeType.YellowDark,
			ThemeType.CyanLight,
			ThemeType.CyanDark,
			ThemeType.BlueLight,
			ThemeType.BlueDark
		};

		/// <summary>v3.1.1：纯色皮肤（红/橙/黄/绿/青/蓝/紫，每种含深浅），
		/// 按彩虹色排序。外观菜单里收拢到"纯色"二级菜单。</summary>
		public static readonly IReadOnlyList<ThemeType> SolidColorThemes = new ThemeType[]
		{
			ThemeType.RedLight,
			ThemeType.RedDark,
			ThemeType.OrangeLight,
			ThemeType.OrangeDark,
			ThemeType.YellowLight,
			ThemeType.YellowDark,
			ThemeType.GreenLight,
			ThemeType.GreenDark,
			ThemeType.CyanLight,
			ThemeType.CyanDark,
			ThemeType.BlueLight,
			ThemeType.BlueDark,
			ThemeType.PurpleLight,
			ThemeType.PurpleDark
		};

		/// <summary>v3.1.1：判断是否纯色皮肤（用于"纯色"父菜单项的 IsChecked）。</summary>
		public static bool IsSolidColor(this ThemeType themeType)
		{
			ThemeType[] solid = new ThemeType[]
			{
				ThemeType.RedLight, ThemeType.RedDark,
				ThemeType.OrangeLight, ThemeType.OrangeDark,
				ThemeType.YellowLight, ThemeType.YellowDark,
				ThemeType.GreenLight, ThemeType.GreenDark,
				ThemeType.CyanLight, ThemeType.CyanDark,
				ThemeType.BlueLight, ThemeType.BlueDark,
				ThemeType.PurpleLight, ThemeType.PurpleDark
			};
			return Array.IndexOf(solid, themeType) >= 0;
		}
	}
}
