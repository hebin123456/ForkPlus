using Avalonia.Media;

namespace ForkPlus.UI
{
	/// <summary>
	/// UI 层字体常量，从 Consts.Fonts 移出以消除业务层的 WPF 依赖。
	/// 阶段 4 里程碑 4.7-a：System.Windows.Media.FontFamily → Avalonia.Media.FontFamily。
	/// </summary>
	public static class FontConstants
	{
		public static readonly FontFamily MonospaceFontFamily = new FontFamily(Consts.Fonts.Monospace);
		public static readonly FontFamily ProportionalFontFamily = new FontFamily(Consts.Fonts.Proportional);
	}
}
