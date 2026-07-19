using System;
using System.Linq;
using ForkPlus.UI;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.1.1 主题扩展单元测试。覆盖 SkinName（所有枚举值）、IsDarkBase、IsSolidColor、
	/// AllThemes / SolidColorThemes 一致性。重点回归 v3.1.1 新增的 10 个纯色主题。
	/// </summary>
	public class ThemeTypeExtensionsTests
	{
		[Theory]
		[InlineData(ThemeType.Light, "Light")]
		[InlineData(ThemeType.Dark, "Dark")]
		[InlineData(ThemeType.SolarizedLight, "SolarizedLight")]
		[InlineData(ThemeType.SolarizedDark, "SolarizedDark")]
		[InlineData(ThemeType.Dracula, "Dracula")]
		[InlineData(ThemeType.GitHubLight, "GitHubLight")]
		[InlineData(ThemeType.GitHubDark, "GitHubDark")]
		[InlineData(ThemeType.Monokai, "Monokai")]
		[InlineData(ThemeType.PurpleLight, "PurpleLight")]
		[InlineData(ThemeType.PurpleDark, "PurpleDark")]
		[InlineData(ThemeType.GreenLight, "GreenLight")]
		[InlineData(ThemeType.GreenDark, "GreenDark")]
		// v3.1.1 新增纯色主题
		[InlineData(ThemeType.RedLight, "RedLight")]
		[InlineData(ThemeType.RedDark, "RedDark")]
		[InlineData(ThemeType.OrangeLight, "OrangeLight")]
		[InlineData(ThemeType.OrangeDark, "OrangeDark")]
		[InlineData(ThemeType.YellowLight, "YellowLight")]
		[InlineData(ThemeType.YellowDark, "YellowDark")]
		[InlineData(ThemeType.CyanLight, "CyanLight")]
		[InlineData(ThemeType.CyanDark, "CyanDark")]
		[InlineData(ThemeType.BlueLight, "BlueLight")]
		[InlineData(ThemeType.BlueDark, "BlueDark")]
		public void SkinName_AllEnumValues_ReturnExpectedString(ThemeType theme, string expected)
		{
			Assert.Equal(expected, theme.SkinName());
		}

		[Theory]
		[InlineData(ThemeType.Light, false)]
		[InlineData(ThemeType.Dark, true)]
		[InlineData(ThemeType.SolarizedLight, false)]
		[InlineData(ThemeType.SolarizedDark, true)]
		[InlineData(ThemeType.Dracula, true)]
		[InlineData(ThemeType.GitHubLight, false)]
		[InlineData(ThemeType.GitHubDark, true)]
		[InlineData(ThemeType.Monokai, true)]
		[InlineData(ThemeType.PurpleLight, false)]
		[InlineData(ThemeType.PurpleDark, true)]
		[InlineData(ThemeType.GreenLight, false)]
		[InlineData(ThemeType.GreenDark, true)]
		[InlineData(ThemeType.RedLight, false)]
		[InlineData(ThemeType.RedDark, true)]
		[InlineData(ThemeType.OrangeLight, false)]
		[InlineData(ThemeType.OrangeDark, true)]
		[InlineData(ThemeType.YellowLight, false)]
		[InlineData(ThemeType.YellowDark, true)]
		[InlineData(ThemeType.CyanLight, false)]
		[InlineData(ThemeType.CyanDark, true)]
		[InlineData(ThemeType.BlueLight, false)]
		[InlineData(ThemeType.BlueDark, true)]
		public void IsDarkBase_AllEnumValues_ReturnsExpectedDarkness(ThemeType theme, bool expected)
		{
			Assert.Equal(expected, theme.IsDarkBase());
		}

		[Theory]
		[InlineData(ThemeType.Light, false)]
		[InlineData(ThemeType.Dark, false)]
		[InlineData(ThemeType.SolarizedLight, false)]
		[InlineData(ThemeType.SolarizedDark, false)]
		[InlineData(ThemeType.Dracula, false)]
		[InlineData(ThemeType.GitHubLight, false)]
		[InlineData(ThemeType.GitHubDark, false)]
		[InlineData(ThemeType.Monokai, false)]
		[InlineData(ThemeType.RedLight, true)]
		[InlineData(ThemeType.RedDark, true)]
		[InlineData(ThemeType.OrangeLight, true)]
		[InlineData(ThemeType.OrangeDark, true)]
		[InlineData(ThemeType.YellowLight, true)]
		[InlineData(ThemeType.YellowDark, true)]
		[InlineData(ThemeType.GreenLight, true)]
		[InlineData(ThemeType.GreenDark, true)]
		[InlineData(ThemeType.CyanLight, true)]
		[InlineData(ThemeType.CyanDark, true)]
		[InlineData(ThemeType.BlueLight, true)]
		[InlineData(ThemeType.BlueDark, true)]
		[InlineData(ThemeType.PurpleLight, true)]
		[InlineData(ThemeType.PurpleDark, true)]
		public void IsSolidColor_DistinguishesSolidFromNonSolid(ThemeType theme, bool expected)
		{
			Assert.Equal(expected, theme.IsSolidColor());
		}

		[Fact]
		public void AllThemes_ContainsExactly22Themes()
		{
			Assert.Equal(22, ThemeTypeExtensions.AllThemes.Count);
		}

		[Fact]
		public void AllThemes_NoDuplicates()
		{
			int uniqueCount = ThemeTypeExtensions.AllThemes.Distinct().Count();
			Assert.Equal(ThemeTypeExtensions.AllThemes.Count, uniqueCount);
		}

		[Fact]
		public void AllThemes_ContainsAllSolidColorThemes()
		{
			foreach (ThemeType theme in ThemeTypeExtensions.SolidColorThemes)
			{
				Assert.Contains(theme, ThemeTypeExtensions.AllThemes);
			}
		}

		[Fact]
		public void SolidColorThemes_ContainsExactly14Themes()
		{
			Assert.Equal(14, ThemeTypeExtensions.SolidColorThemes.Count);
		}

		[Fact]
		public void SolidColorThemes_NoDuplicates()
		{
			int uniqueCount = ThemeTypeExtensions.SolidColorThemes.Distinct().Count();
			Assert.Equal(ThemeTypeExtensions.SolidColorThemes.Count, uniqueCount);
		}

		[Fact]
		public void SolidColorThemes_AllReturnTrueForIsSolidColor()
		{
			// 一致性测试：SolidColorThemes 中每项 IsSolidColor 都返回 true
			foreach (ThemeType theme in ThemeTypeExtensions.SolidColorThemes)
			{
				Assert.True(theme.IsSolidColor(), $"Theme {theme} is in SolidColorThemes but IsSolidColor() returned false");
			}
		}

		[Fact]
		public void AllThemes_AllSolidColors_ReturnTrueForIsSolidColor()
		{
			// 一致性测试：AllThemes 中 IsSolidColor==true 的应等于 SolidColorThemes
			var solidFromAll = ThemeTypeExtensions.AllThemes.Where(t => t.IsSolidColor()).ToList();
			Assert.Equal(solidFromAll.Count, ThemeTypeExtensions.SolidColorThemes.Count);
			foreach (ThemeType theme in solidFromAll)
			{
				Assert.Contains(theme, ThemeTypeExtensions.SolidColorThemes);
			}
		}

		[Fact]
	public void ThemeType_Light_IsZeroForBackwardCompat()
		{
			// 兼容性回归：旧 settings.json 里 Light=0、Dark=1 不能变
			Assert.Equal(0, (int)ThemeType.Light);
			Assert.Equal(1, (int)ThemeType.Dark);
		}

		[Fact]
		public void ThemeType_NewSolidColorValues_AreAfterGreenDark()
		{
			// v3.1.1 新增的纯色主题值应从 12 开始
			Assert.Equal(12, (int)ThemeType.RedLight);
			Assert.Equal(21, (int)ThemeType.BlueDark);
		}

		[Fact]
		public void SkinName_DefaultEnumValue_ReturnsLight()
		{
			// 未定义的枚举值（如 (ThemeType)999）应回退到 "Light"
			ThemeType unknown = (ThemeType)999;
			Assert.Equal("Light", unknown.SkinName());
		}

		[Fact]
		public void IsDarkBase_UnknownEnumValue_ReturnsFalse()
		{
			ThemeType unknown = (ThemeType)999;
			Assert.False(unknown.IsDarkBase());
		}
	}
}
