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
	/// v4.1.4（行间距收紧，两步）：子集字体垂直度量统一收紧到 1.16em（hhea ascent 928 /
	/// descent -232 / lineGap 0，OS/2 win 与 typo 三处一致；v4.1.2 时 1.448em，Win
	/// DWrite 实测 1.619em；第一步先收到 1.25em，配合 LineHeightFactor=1.0 后仍超
	/// 新行槽，再收紧到 1.16em）。行槽公式随 CodeEditor 的修复变化：
	///   v4.1.2：槽 = max(自然高, DefaultTextHeight × 1.16 ≈ 17.66px@Win) —— 1.25em
	///          （16.25px）已足够入槽，同步器不再抬 factor，行间距回到"默认偏宽"；
	///   v4.1.4：CodeEditor/HexEditor 显式 LineHeightFactor=1.0（对齐 WPF 自然行高，
	///          "同区域 36 行 → 43 行"），槽 = ASCII 自然行高本身（Consolas@13 ≈
	///          15.22px@Win / 15.13px@Linux）——1.25em 的 16.25px 再次超槽，会触发
	///          SideBySideLineHeightSynchronizer 把全部行槽抬到 16.25px（比 WPF 的
	///          15.22px 宽 7%）；收紧到 1.16em（15.08px@13px）≤ 两侧平台 ASCII 自然
	///          行高，含中文行不再超槽，行密度与 WPF 3.13.2 一致。
	/// 拆分沿用 4:1（ascent 0.928em / descent 0.232em），汉字墨迹 0.88em 上/0.12em 下
	/// 不裁字；33739 字形中仅 9 个竖排标点（U+3031-3035、U+FFE8 等）越界且横排文本
	/// 不使用。守卫测试：SideBySideRegressionTests.
	/// CjkFallbackFontMetrics_FitDefaultLineSlot_SpacingNotInflated（若子集工具重建
	/// 字体丢失度量修正将在此失败）。
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
