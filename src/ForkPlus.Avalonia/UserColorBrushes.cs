using System.Collections.Generic;
using Avalonia.Media;
using ForkPlus.UI;

// Avalonia spike 版 UserColorBrushes（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/UserColorBrushes.cs（77 行）：
//   - WPF: internal class UserColorBrushes
//   - 4 组静态字段：_userBorderBrushesLight/Dark（各 49 个 SolidColorBrush）+ _lightOpacityValues/_darkOpacityValues
//   - 12 个 _userBorderColors（Color.FromRgb）
//   - AllBrushes(ThemeType) / GetBrush(byte index, ThemeType)
//   - CreateBrushes(Color blendColor, int[] opacityValues)：49 个 brush（含 null 占位）
//   - CreateBrush(Color, Color, int)：opacity 混合
//   - 依赖：System.Windows.Media.SolidColorBrush / Color / ForkPlus.UI.ThemeType
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF System.Windows.Media.SolidColorBrush → Avalonia.Media.SolidColorBrush
//   2. WPF Color.FromRgb(r, g, b) → Avalonia Color.FromRgb(r, g, b)（API 一致）
//   3. WPF Colors.White / Colors.Black → Avalonia Colors.White / Colors.Black
//   4. WPF brush.Freeze() → Avalonia 无此方法（跳过）
//   5. WPF ThemeType → ForkPlus.UI.ThemeType（Core 可用）
//
// spike 简化（task spec 关键 API）：
//   - AllBrushes / GetBrush / CreateBrushes / CreateBrush
namespace ForkPlus.Avalonia
{
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
