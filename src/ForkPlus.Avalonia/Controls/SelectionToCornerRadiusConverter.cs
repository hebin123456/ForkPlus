using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 SelectionToCornerRadiusConverter（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/SelectionToCornerRadiusConverter.cs（44 行）：
    //   - WPF SelectionToCornerRadiusConverter : MarkupExtension, IValueConverter
    //   - 4 个静态 CornerRadius 字段：None/All/Top/Bottom
    //   - Convert(object value) → CornerRadius
    //   - ConvertBack → throw NotImplementedException
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IValueConverter → Avalonia.Data.Converters.IValueConverter
    //   2. WPF System.Windows.CornerRadius → Avalonia.CornerRadius
    //   3. WPF MarkupExtension → spike 跳过，用 Instance 静态单例
    public class SelectionToCornerRadiusConverter : IValueConverter
    {
        private static readonly CornerRadius CornerRadiusNone = new CornerRadius(0.0, 0.0, 0.0, 0.0);
        private static readonly CornerRadius CornerRadiusAll = new CornerRadius(4.0, 4.0, 4.0, 4.0);
        private static readonly CornerRadius CornerRadiusTop = new CornerRadius(4.0, 4.0, 0.0, 0.0);
        private static readonly CornerRadius CornerRadiusBottom = new CornerRadius(0.0, 0.0, 4.0, 4.0);

        public static readonly SelectionToCornerRadiusConverter Instance = new SelectionToCornerRadiusConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ListBoxSelectionType))
            {
                return CornerRadiusAll;
            }
            return (ListBoxSelectionType)value switch
            {
                ListBoxSelectionType.None => CornerRadiusNone,
                ListBoxSelectionType.Middle => CornerRadiusNone,
                ListBoxSelectionType.Top => CornerRadiusTop,
                ListBoxSelectionType.Bottom => CornerRadiusBottom,
                _ => CornerRadiusAll,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
