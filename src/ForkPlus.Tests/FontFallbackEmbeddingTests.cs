// v4.0.5（2026-09-09）字体内置回归测试：无 CJK 字体环境（ARM/minimal Linux）不再渲染方框。
// 配置：FontSetup.CreateFontManagerOptions()（Program 与 HeadlessAppBootstrap 共用），
// 内嵌 Noto Sans CJK SC 子集（Regular+Bold，Assets/Fonts）经 FontFallbacks 全局回退。
//
// 三层验证：
//   1) 资源可加载：文件夹 URI 解析出 Regular/Bold 两个字重（缺字重则粗体 CJK 退化为合成加粗）；
//   2) 字形覆盖：核心 CJK（汉字/假名/谚文/全角标点/圈号）在 cmap 中可查——这是"无方框"的直接保证；
//      且 Latin 不在 cmap 中（Latin 由 Inter 供给，内嵌字体不改变西文渲染度量）；
//   3) 回退链端到端：FontManager.TryMatchCharacter（Avalonia 字形回退的公开解析入口）
//      以 Inter 为主字体查 CJK 字符，应命中含该字形的 typeface。
using System;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Fonts.Tables.Cmap;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class FontFallbackEmbeddingTests
	{
		private readonly ITestOutputHelper _output;

		public FontFallbackEmbeddingTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void EmbeddedCjkFont_LoadsBothWeights()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var family = new FontFamily(FontSetup.CjkFallbackFontUri);
				foreach (FontWeight weight in new[] { FontWeight.Normal, FontWeight.Bold })
				{
					var typeface = new Typeface(family, FontStyle.Normal, weight);
					GlyphTypeface? glyphTypeface = typeface.GlyphTypeface;
					Assert.NotNull(glyphTypeface);
					Assert.Equal(weight, glyphTypeface!.Weight);
					_output.WriteLine($"{weight}: family='{glyphTypeface.FamilyName}', glyphs={glyphTypeface.GlyphCount}");
				}
			});
		}

		[Theory]
		[InlineData(0x4ED3)] // 仓
		[InlineData(0x5E93)] // 库
		[InlineData(0x8BBE)] // 设
		[InlineData(0x7F6E)] // 置
		[InlineData(0x3042)] // あ
		[InlineData(0x30A2)] // ア
		[InlineData(0xD55C)] // 한
		[InlineData(0xFF0C)] // ，
		[InlineData(0x3002)] // 。
		[InlineData(0x2460)] // ①
		[InlineData(0x4E00)] // 一（U+4E00 区间起点）
		[InlineData(0x9FA5)] // 龥（CJK 块经典末位；0x9FFF 是 Unicode 14 新增，2022 版 Noto 无此字形）
		public void EmbeddedCjkFont_CoversCoreGlyphs(uint codepoint)
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var typeface = new Typeface(new FontFamily(FontSetup.CjkFallbackFontUri));
				GlyphTypeface? glyphTypeface = typeface.GlyphTypeface;
				Assert.NotNull(glyphTypeface);
				Assert.True(glyphTypeface!.CharacterToGlyphMap.ContainsGlyph((int)codepoint),
					$"0x{codepoint:X4} 不在内嵌字体 cmap 中（该字符将渲染为方框）");
			});
		}

		[Fact]
		public void EmbeddedCjkFont_ExcludesLatin()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var typeface = new Typeface(new FontFamily(FontSetup.CjkFallbackFontUri));
				GlyphTypeface? glyphTypeface = typeface.GlyphTypeface;
				Assert.NotNull(glyphTypeface);
				// Latin 由 Inter 覆盖（FontFallback 仅在主字体缺字形时触发）。
				// 若 Latin 混进内嵌 cmap，西文字形度量将受回退影响，属打包错误。
				Assert.False(glyphTypeface!.CharacterToGlyphMap.ContainsGlyph('A'), "Latin 不应包含在内嵌 CJK 子集中");
				Assert.False(glyphTypeface.CharacterToGlyphMap.ContainsGlyph('z'), "Latin 不应包含在内嵌 CJK 子集中");
			});
		}

		[Fact]
		public void FontManagerFallback_ResolvesCjkGlyphForInter()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				// 以 Inter（生产主字体）发起回退解析——这是 Avalonia 文本整形缺字形时
				// 的公开解析入口（FontFamily 逗号列表不做逐字形回退，issue #19362）。
				var inter = new FontFamily("Inter");
				bool matched = FontManager.Current.TryMatchCharacter(
					0x4ED3 /* 仓 */, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
					inter, CultureInfo.GetCultureInfo("zh-Hans"), out Typeface typeface);
				Assert.True(matched, "CJK 字符应命中回退 typeface（否则渲染为方框）");
				GlyphTypeface? glyphTypeface = typeface.GlyphTypeface;
				Assert.NotNull(glyphTypeface);
				Assert.True(glyphTypeface!.CharacterToGlyphMap.ContainsGlyph(0x4ED3),
					$"命中的 typeface（family='{typeface.FontFamily?.Name}'）应包含该字形");
				_output.WriteLine($"0x4ED3 回退命中: family='{typeface.FontFamily?.Name}' weight={typeface.Weight}");
			});
		}
	}
}
