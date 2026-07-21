using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 NullToVisibilityConverter（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/NullToVisibilityConverter.cs（25 行）：
    //   - WPF NullToVisibilityConverter : MarkupExtension, IValueConverter
    //   - Convert(object value) → (value == null) ? Visibility.Collapsed : Visibility.Visible
    //   - ConvertBack(...) → null
    //   - ProvideValue(IServiceProvider) → this
    //     （MarkupExtension 模式，支持 {Binding Converter={local:NullToVisibilityConverter}}）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IValueConverter → Avalonia.Data.Converters.IValueConverter
    //      （签名一致：Convert + ConvertBack + targetType + parameter + culture）
    //   2. WPF System.Windows.Markup.MarkupExtension → spike 跳过
    //      （Avalonia 不使用 MarkupExtension 实现转换器，
    //       改用静态 Instance 单例 + {Binding Converter={x:Static local:NullToVisibilityConverter.Instance}}）
    //   3. WPF Visibility.Collapsed / Visibility.Visible → spike 返回 bool
    //      （Avalonia 用 IsVisible = false / true 替代 WPF Visibility.Collapsed / Visible）
    //      null → false（隐藏）/ non-null → true（可见）
    //   4. spike 提供 Instance 静态单例
    //
    // spike 简化（task spec 关键 API）：
    //   - 实现 Avalonia.Data.Converters.IValueConverter
    //   - Convert：null → false（隐藏）/ non-null → true（可见）
    //   - ConvertBack：null
    //   - Instance 静态单例
    public class NullToVisibilityConverter : IValueConverter
    {
        // spike 新增：Avalonia 转换器常用静态单例模式
        public static readonly NullToVisibilityConverter Instance = new NullToVisibilityConverter();

        // 对照 WPF: public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        //   return (value == null) ? Visibility.Collapsed : Visibility.Visible;
        // spike 版：Visibility.Collapsed → false / Visibility.Visible → true
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        // 对照 WPF: public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        //   return null;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
