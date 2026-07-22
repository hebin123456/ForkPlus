using Avalonia.Media;

// Avalonia spike 版 FontConstants（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/FontConstants.cs（13 行）：
//   - WPF: public static class FontConstants
//   - MonospaceFontFamily / ProportionalFontFamily（System.Windows.Media.FontFamily）
//   - 取值来自 Consts.Fonts.Monospace / Consts.Fonts.Proportional（Core）
//
// Avalonia 版差异：
//   1. WPF System.Windows.Media.FontFamily → Avalonia.Media.FontFamily
//      （构造函数签名一致：FontFamily(string familyName)）
//   2. Consts 来源不变（ForkPlus.Core，namespace ForkPlus，从 ForkPlus.Avalonia
//      可经外层命名空间直接解析 ForkPlus.Consts）
//
// spike 简化：与 WPF 一致的两个 FontFamily 只读字段。
namespace ForkPlus.Avalonia
{
	/// <summary>UI 层字体常量，从 Consts.Fonts 移出以消除业务层的 WPF 依赖。
	/// Avalonia 版替换为 Avalonia 原生字体 API。</summary>
	public static class FontConstants
	{
		public static readonly FontFamily MonospaceFontFamily = new FontFamily(Consts.Fonts.Monospace);

		public static readonly FontFamily ProportionalFontFamily = new FontFamily(Consts.Fonts.Proportional);
	}
}
