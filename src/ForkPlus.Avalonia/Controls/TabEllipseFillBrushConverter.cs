using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 TabEllipseFillBrushConverter（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/TabEllipseFillBrushConverter.cs（35 行）：
    //   - WPF TabEllipseFillBrushConverter : MarkupExtension, IMultiValueConverter
    //   - Convert(object[] values, ...) → IBrush
    //     - values.Length < 2 → Brushes.Transparent
    //     - values[0] = SolidColorBrush, values[1] = bool
    //     - bool==false → Brushes.Transparent
    //     - 否则 → solidColorBrush ?? ClosableTabItem.IsDirtyDefaultBrush
    //   - ConvertBack → throw NotSupportedException
    //   - ProvideValue → this（MarkupExtension 模式）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IMultiValueConverter → Avalonia.Data.Converters.IMultiValueConverter
    //      （签名差异：WPF object[] → Avalonia IList<object>）
    //   2. WPF System.Windows.Markup.MarkupExtension → spike 跳过
    //      （Avalonia 不使用 MarkupExtension 实现转换器，改用静态 Instance 单例）
    //   3. WPF Brushes.Transparent → Avalonia.Media.Brushes.Transparent
    //   4. WPF ClosableTabItem.IsDirtyDefaultBrush → spike 跳过（spike ClosableTabItem 未定义此字段）
    //      用 Brushes.Transparent 兜底（语义上"未设置 dirty 时透明"）
    //   5. spike 提供 Instance 静态单例
    //   6. spike 增加 null/类型容错（values[0] 非 IBrush 时返回 Transparent）
    //
    // spike 简化：
    //   - 实现 Avalonia.Data.Converters.IMultiValueConverter
    //   - Convert：根据 IsDirty + TagBrush 返回填充画刷
    //   - Instance 静态单例
    public class TabEllipseFillBrushConverter : IMultiValueConverter
    {
        // spike 新增：Avalonia 转换器常用静态单例模式
        public static readonly TabEllipseFillBrushConverter Instance = new TabEllipseFillBrushConverter();

        // 对照 WPF: public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
            {
                return Brushes.Transparent;
            }
            // 对照 WPF: SolidColorBrush solidColorBrush = (SolidColorBrush)values[0];
            // spike 版用 IBrush 接收（Avalonia 用 IBrush 替代 WPF Brush）
            IBrush brush = values[0] as IBrush;
            // 对照 WPF: if (!(bool)values[1]) return Brushes.Transparent;
            if (values[1] is bool b && !b)
            {
                return Brushes.Transparent;
            }
            // 对照 WPF: return solidColorBrush ?? ClosableTabItem.IsDirtyDefaultBrush;
            // spike 版：ClosableTabItem.IsDirtyDefaultBrush 未迁移，用 Brushes.Transparent 兜底
            return brush ?? Brushes.Transparent;
        }
    }
}
