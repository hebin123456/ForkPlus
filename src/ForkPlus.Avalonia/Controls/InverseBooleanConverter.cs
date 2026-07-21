using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 InverseBooleanConverter（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/InverseBooleanConverter.cs（20 行）：
    //   - WPF InverseBooleanConverter : IValueConverter
    //   - [ValueConversion(typeof(bool), typeof(Visibility))]  // WPF 标记，Avalonia 无对应
    //   - Convert(object value) → !(bool)value
    //   - ConvertBack(...) → null
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IValueConverter → Avalonia.Data.Converters.IValueConverter
    //      （签名一致：Convert + ConvertBack + targetType + parameter + culture）
    //   2. WPF [ValueConversion] Attribute → spike 跳过（Avalonia 无对应 Attribute）
    //   3. WPF Visibility 返回值 → spike 仍返回 bool
    //      （WPF 标记说返回 Visibility，但实际 Convert 返回 !(bool)value，是 bool；
    //       spike 保持 bool 返回值，与实际行为一致）
    //   4. spike 提供 Instance 静态单例
    //      （Avalonia 转换器常用模式，便于 {Binding Converter={x:Static ...}}）
    //   5. spike Convert 增加 null/类型容错（value 非 bool 时返回 false）
    //
    // spike 简化（task spec 关键 API）：
    //   - 实现 Avalonia.Data.Converters.IValueConverter
    //   - Convert：!(bool)value（含类型容错）
    //   - ConvertBack：null
    //   - Instance 静态单例
    public class InverseBooleanConverter : IValueConverter
    {
        // spike 新增：Avalonia 转换器常用静态单例模式
        public static readonly InverseBooleanConverter Instance = new InverseBooleanConverter();

        // 对照 WPF: public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        //   return !(bool)value;
        // spike 版：增加类型容错（value 非 bool 时返回 false，避免 InvalidCastException）
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return false;
        }

        // 对照 WPF: public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        //   return null;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
