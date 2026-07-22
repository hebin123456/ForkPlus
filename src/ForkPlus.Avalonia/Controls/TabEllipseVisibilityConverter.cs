using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 TabEllipseVisibilityConverter（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/TabEllipseVisibilityConverter.cs（32 行）：
    //   - WPF TabEllipseVisibilityConverter : MarkupExtension, IMultiValueConverter
    //   - Convert(object[] values, ...) → Visibility
    //     - values.Length < 2 → Visibility.Collapsed
    //     - values[0] = SolidColorBrush, values[1] = bool
    //     - bool==true → Visibility.Visible
    //     - bool==false → (solidColorBrush==null) ? Collapsed : Visible
    //   - ConvertBack → throw NotSupportedException
    //   - ProvideValue → this（MarkupExtension 模式）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IMultiValueConverter → Avalonia.Data.Converters.IMultiValueConverter
    //      （签名差异：WPF object[] → Avalonia IList<object>）
    //   2. WPF System.Windows.Markup.MarkupExtension → spike 跳过
    //      （Avalonia 不使用 MarkupExtension 实现转换器，改用静态 Instance 单例）
    //   3. WPF Visibility.Collapsed / Visibility.Visible → spike 返回 bool
    //      （Avalonia 用 IsVisible = false / true 替代 WPF Visibility.Collapsed / Visible）
    //   4. spike 提供 Instance 静态单例
    //   5. spike 增加 null/类型容错
    //
    // spike 简化：
    //   - 实现 Avalonia.Data.Converters.IMultiValueConverter
    //   - Convert：返回 bool（Visible=true / Collapsed=false）
    //   - Instance 静态单例
    public class TabEllipseVisibilityConverter : IMultiValueConverter
    {
        // spike 新增：Avalonia 转换器常用静态单例模式
        public static readonly TabEllipseVisibilityConverter Instance = new TabEllipseVisibilityConverter();

        // 对照 WPF: public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        //   return (!(bool)values[1]) ? ((solidColorBrush == null) ? Visibility.Collapsed : Visibility.Visible) : Visibility.Visible;
        // spike 版：Visibility.Visible → true / Visibility.Collapsed → false
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
            {
                // 对照 WPF: return Visibility.Collapsed;
                return false;
            }
            IBrush brush = values[0] as IBrush;
            bool isDirty = values[1] is bool b && b;
            if (isDirty)
            {
                // 对照 WPF: Visibility.Visible
                return true;
            }
            // 对照 WPF: (solidColorBrush == null) ? Visibility.Collapsed : Visibility.Visible
            return brush != null;
        }
    }
}
