using System.Windows.Media;

namespace ForkPlus.UI
{
	/// <summary>
	/// UI 层字体常量，从 Consts.Fonts 移出以消除业务层的 WPF 依赖。
	/// 迁移到 Avalonia 时替换为 Avalonia 原生字体 API。
	/// </summary>
	public static class FontConstants
	{
		public static readonly FontFamily MonospaceFontFamily = new FontFamily(Consts.Fonts.Monospace);
		public static readonly FontFamily ProportionalFontFamily = new FontFamily(Consts.Fonts.Proportional);
	}
}
