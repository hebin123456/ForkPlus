using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 PlacementRectangleConverter（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/PlacementRectangleConverter.cs（28 行）：
    //   - WPF PlacementRectangleConverter : IMultiValueConverter
    //   - public Thickness Margin { get; set; }
    //   - Convert(object[] values) → Rect
    //     if (values.Length == 2 && values[0] is double && values[1] is double)
    //       Point p1 = new Point(Margin.Left, Margin.Top)
    //       Point p2 = new Point(values[0] - Margin.Right, values[1] - Margin.Bottom)
    //       return new Rect(p1, p2)
    //     else return Rect.Empty
    //   - ConvertBack → throw NotSupportedException
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IMultiValueConverter → Avalonia.Data.Converters.IMultiValueConverter
    //      （签名差异：WPF object[] → Avalonia IList<object>）
    //   2. WPF System.Windows.Thickness → Avalonia.Thickness（API 一致）
    //   3. WPF System.Windows.Point → Avalonia.Point（API 一致）
    //   4. WPF System.Windows.Rect → Avalonia.Rect（API 一致）
    //   5. WPF Rect.Empty → spike 用 Avalonia.Rect.Default（语义等价）
    //   6. spike 提供 Instance 静态单例
    //
    // spike 简化：
    //   - 实现 Avalonia.Data.Converters.IMultiValueConverter
    //   - Convert：双 double → Rect（按 Margin 偏移）
    //   - ConvertBack：throw NotSupportedException
    //   - Instance 静态单例
    public class PlacementRectangleConverter : IMultiValueConverter
    {
        // 对照 WPF: public Thickness Margin { get; set; }
        public Thickness Margin { get; set; }

        // spike 新增：Avalonia 转换器常用静态单例模式
        public static readonly PlacementRectangleConverter Instance = new PlacementRectangleConverter();

        // 对照 WPF: public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Count == 2 && values[0] is double num && values[1] is double num2)
            {
                Point point = new Point(Margin.Left, Margin.Top);
                Point point2 = new Point(num - Margin.Right, num2 - Margin.Bottom);
                return new Rect(point, point2);
            }
            // 对照 WPF: return Rect.Empty;
            // Avalonia 11 无 Rect.Empty/Default，用 default(Rect)（空矩形）
            return default(Rect);
        }

        // 对照 WPF: public object[] ConvertBack(...) → throw new NotSupportedException()
        // Avalonia IMultiValueConverter.ConvertBack 返回 object，spike 改 throw
        public object ConvertBack(IList<object> value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
