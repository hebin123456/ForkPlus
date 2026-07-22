using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 InverseBooleanToVisibilityConverter（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/InverseBooleanToVisibilityConverter.cs（21 行）：
    //   - WPF InverseBooleanToVisibilityConverter : IValueConverter
    //   - [ValueConversion(typeof(bool), typeof(Visibility))]
    //   - Convert(object value) → ((bool)value) ? Visibility.Collapsed : Visibility.Visible
    //   - ConvertBack(...) → null
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IValueConverter → Avalonia.Data.Converters.IValueConverter
    //   2. WPF [ValueConversion] Attribute → spike 跳过（Avalonia 无对应 Attribute）
    //   3. WPF Visibility.Collapsed / Visibility.Visible → spike 返回 bool
    //      （Avalonia 用 IsVisible = false / true 替代 WPF Visibility.Collapsed / Visible）
    //   4. spike 提供 Instance 静态单例
    //   5. spike Convert 增加 null/类型容错（value 非 bool 时返回 true）
    //
    // spike 简化：
    //   - 实现 Avalonia.Data.Converters.IValueConverter
    //   - Convert：bool → !bool（Visible=true / Collapsed=false）
    //   - ConvertBack：null
    //   - Instance 静态单例
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        // spike 新增：Avalonia 转换器常用静态单例模式
        public static readonly InverseBooleanToVisibilityConverter Instance = new InverseBooleanToVisibilityConverter();

        // 对照 WPF: public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        //   return ((bool)value) ? Visibility.Collapsed : Visibility.Visible;
        // spike 版：Visibility.Collapsed → false / Visibility.Visible → true
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                // 对照 WPF: bool=true → Collapsed → false; bool=false → Visible → true
                return !b;
            }
            // spike 新增：非 bool 类型返回 true（Visible，避免 InvalidCastException）
            return true;
        }

        // 对照 WPF: public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        //   return null;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
