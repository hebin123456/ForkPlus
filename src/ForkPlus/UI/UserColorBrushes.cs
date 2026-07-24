using System.Collections.Generic;
using Avalonia.Media;

namespace ForkPlus.UI
{
	// 阶段 4.5：WPF System.Windows.Media.Color/SolidColorBrush → Avalonia.Media.Color/SolidColorBrush。
	// WPF Colors.White/Colors.Black → Avalonia Colors.White/Colors.Black（同名同值）。
	// WPF SolidColorBrush.Freeze() → 移除（Avalonia 画刷默认不可变，无需冻结）。
	internal class UserColorBrushes
	{
		private static readonly SolidColorBrush[] _userBorderBrushesLight;

		private static readonly SolidColorBrush[] _userBorderBrushesDark;

		private static readonly int[] _lightOpacityValues;

		private static readonly int[] _darkOpacityValues;

		private static readonly Color[] _userBorderColors;

		static UserColorBrushes()
		{
			_lightOpacityValues = new int[4] { 100, 70, 50, 30 };
			_darkOpacityValues = new int[4] { 100, 80, 60, 40 };
			_userBorderColors = new Color[12]
			{
				Color.FromRgb(230, 61, 12),
				Color.FromRgb(byte.MaxValue, 153, 33),
				Color.FromRgb(202, 198, 27),
				Color.FromRgb(126, 199, 29),
				Color.FromRgb(27, 197, 41),
				Color.FromRgb(27, 197, 127),
				Color.FromRgb(15, 204, 191),
				Color.FromRgb(35, 187, 252),
				Color.FromRgb(67, 119, 226),
				Color.FromRgb(99, 72, 218),
				Color.FromRgb(158, 50, 212),
				Color.FromRgb(222, 15, 213)
			};
			_userBorderBrushesLight = CreateBrushes(Colors.White, _lightOpacityValues);
			_userBorderBrushesDark = CreateBrushes(Colors.Black, _darkOpacityValues);
		}

		public SolidColorBrush[] AllBrushes(ThemeType theme)
		{
			if (!theme.IsDarkBase()) { return _userBorderBrushesLight; }
			return _userBorderBrushesDark;
		}

		public SolidColorBrush GetBrush(byte index, ThemeType theme)
		{
			if (!theme.IsDarkBase()) { return _userBorderBrushesLight[index]; }
			return _userBorderBrushesDark[index];
		}

		private static SolidColorBrush[] CreateBrushes(Color blendColor, int[] opacityValues)
		{
			List<SolidColorBrush> list = new List<SolidColorBrush>(49);
			list.Add(null);
			foreach (int opacity in opacityValues)
			{
				Color[] userBorderColors = _userBorderColors;
				for (int j = 0; j < userBorderColors.Length; j++)
				{
					SolidColorBrush solidColorBrush = CreateBrush(userBorderColors[j], blendColor, opacity);
					// 阶段 4.5：Avalonia 画刷默认不可变，无需 WPF Freeze()。
					list.Add(solidColorBrush);
				}
			}
			return list.ToArray();
		}

		private static SolidColorBrush CreateBrush(Color color, Color blendColor, int opacity)
		{
			byte r = (byte)(opacity * (color.R - blendColor.R) / 100 + blendColor.R);
			byte g = (byte)(opacity * (color.G - blendColor.G) / 100 + blendColor.G);
			byte b = (byte)(opacity * (color.B - blendColor.B) / 100 + blendColor.B);
			return new SolidColorBrush(Color.FromRgb(r, g, b));
		}
	}
}
