// ⚠ 临时桥接类型 ─ 阶段 4.5 编译过渡用。
// WPF System.Windows.Media.FontWeights / FontStyles / FontStretches 是静态类，
// 暴露 FontWeights.Normal / FontWeights.Bold 等静态属性。
// Avalonia 11.3.18 仅提供单数 struct FontWeight / FontStyle / FontStretch，
// 其静态字段 Normal / Bold / Italic 等可直接复用。
// 本文件在 Avalonia.Media 命名空间下补齐三个复数静态类作为桥接，
// 让原 WPF 代码无需修改即可通过编译。
using Avalonia.Media;

namespace Avalonia.Media
{
	public static class FontWeights
	{
		public static FontWeight Thin => FontWeight.Thin;
		public static FontWeight ExtraLight => FontWeight.ExtraLight;
		public static FontWeight Light => FontWeight.Light;
		public static FontWeight SemiLight => FontWeight.SemiLight;
		public static FontWeight Normal => FontWeight.Normal;
		public static FontWeight Medium => FontWeight.Medium;
		public static FontWeight SemiBold => FontWeight.SemiBold;
		public static FontWeight Bold => FontWeight.Bold;
		public static FontWeight ExtraBold => FontWeight.ExtraBold;
		public static FontWeight Black => FontWeight.Black;
		public static FontWeight ExtraBlack => FontWeight.ExtraBlack;
	}

	public static class FontStyles
	{
		public static FontStyle Normal => FontStyle.Normal;
		public static FontStyle Italic => FontStyle.Italic;
		public static FontStyle Oblique => FontStyle.Oblique;
	}

	public static class FontStretches
	{
		public static FontStretch UltraCondensed => FontStretch.UltraCondensed;
		public static FontStretch ExtraCondensed => FontStretch.ExtraCondensed;
		public static FontStretch Condensed => FontStretch.Condensed;
		public static FontStretch SemiCondensed => FontStretch.SemiCondensed;
		public static FontStretch Normal => FontStretch.Normal;
		public static FontStretch SemiExpanded => FontStretch.SemiExpanded;
		public static FontStretch Expanded => FontStretch.Expanded;
		public static FontStretch ExtraExpanded => FontStretch.ExtraExpanded;
		public static FontStretch UltraExpanded => FontStretch.UltraExpanded;
	}
}
