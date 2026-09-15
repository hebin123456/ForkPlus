using System;
using Avalonia.Media;

namespace ForkPlus
{
	/// <summary>
	/// v4.0.5 全局字体配置：内置 Noto Sans CJK SC 子集（Regular+Bold 双字重，
	/// Assets/Fonts，共 ~25MB）作为字形级回退，修复无 CJK 字体环境（ARM/minimal
	/// Linux 等）的中日韩文本渲染为方框问题。
	///
	/// 根因链：主字体 Inter 仅 Latin 字形；样式里硬编码的 "Segoe UI"/"Consolas"
	/// 在 Linux 上不存在，文本落到系统默认字体后没有 CJK 字形可回退。
	/// 机制：FontManagerOptions.FontFallbacks 是 Avalonia 官方全局回退机制
	/// （FontFamily 逗号列表不做逐字形回退，见 Avalonia issue #19362）。主字体缺
	/// 字形时按 UnicodeRange 命中内嵌字体；两字重经文件夹 URI 聚合为同一家族，
	/// 粗体 CJK 渲染真 Bold 而非合成加粗。未覆盖的稀有区间（CJK 扩展 A 等）不在
	/// Range 内，继续走系统字体链，与现状一致不劣化。
	///
	/// 本类被 Program.BuildAvaloniaApp 与测试启动器 HeadlessAppBootstrap 共用，
	/// 保证测试环境的字体解析与生产一致（字形度量一致，像素级回归不受影响）。
	///
	/// v4.1.3（行间距收紧）：子集字体垂直度量统一收紧到 1.25em（hhea ascent 1000 /
	/// descent -250 / lineGap 0，OS/2 win 与 typo 三处一致；原 1.448em，Win DWrite
	/// 实测 1.619em）。原度量使含中文的 AvaloniaEdit 行自然高（16.25→18.82/21.05px
	/// @13px）超默认行槽（ASCII 自然高 × 1.16 ≈ 17.55/17.66px），v4.1.2 的
	/// SideBySideLineHeightSynchronizer 被迫把全部行槽抬到 CJK 自然高 → 行间距过宽。
	/// 收紧后 CJK 自然高 16.25px ≤ 默认槽，行槽回到统一默认值（v4.1.2 之前的间距），
	/// 左右天然等高。汉字墨迹 0.88em 上/0.12em 下不裁字；33739 字形中仅 9 个竖排
	/// 标点（U+3031-3035、U+FFE8 等）越界且横排文本不使用。守卫测试：
	/// SideBySideRegressionTests.CjkFallbackFontMetrics_FitDefaultLineSlot_SpacingNotInflated
	///（若子集工具重建字体丢失度量修正将在此失败）。
	/// </summary>
	public static class FontSetup
	{
		/// <summary>内嵌 CJK 回退字体的资源 URI（文件夹形式，两字重聚合为同一家族）。</summary>
		public const string CjkFallbackFontUri = "avares://ForkPlus/Assets/Fonts#Noto Sans CJK SC";

		/// <summary>
		/// 回退触发的 Unicode 区间，与子集生成时保留的区间一一对应：
		/// CJK 统一表意(4E00-9FFF)、谚文音节(AC00-D7AF)、假名、CJK 标点、
		/// 全角/半角形式、圈号数字、罗马数字、注音、兼容表意、CJK 部首补充。
		/// （U+00B7/U+2015/U+2018-201D/U+2026 为中文排版高频标点，Inter 虽有
		/// 弯引号但样式与 CJK 排版不协调，统一由内嵌字体供给。）
		/// </summary>
		public const string CjkFallbackUnicodeRange =
			"U+00B7, U+2015, U+2018-201D, U+2026, U+2160-217F, U+2460-24FF, U+2E80-2EFF, " +
			"U+3000-303F, U+3040-309F, U+30A0-30FF, U+3100-312F, U+3130-318F, U+31F0-31FF, " +
			"U+3200-32FF, U+4E00-9FFF, U+AC00-D7AF, U+F900-FAFF, U+FE30-FE4F, U+FF00-FFEF";

		public static FontManagerOptions CreateFontManagerOptions()
		{
			return new FontManagerOptions
			{
				FontFallbacks = new FontFallback[]
				{
					new FontFallback
					{
						FontFamily = new FontFamily(CjkFallbackFontUri),
						UnicodeRange = UnicodeRange.Parse(CjkFallbackUnicodeRange)
					}
				}
			};
		}
	}
}
