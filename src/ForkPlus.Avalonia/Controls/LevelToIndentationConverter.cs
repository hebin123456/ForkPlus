using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 LevelToIndentationConverter（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/LevelToIndentationConverter.cs（26 行）：
    //   - WPF LevelToIndentationConverter : IValueConverter
    //   - private static readonly double Offset = 10.0
    //   - Convert(object value) → double
    //     if (value is int) return (double)((int)value - 1) * Offset;
    //     else return 0.0
    //   - ConvertBack → throw NotSupportedException
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IValueConverter → Avalonia.Data.Converters.IValueConverter
    //   2. spike 提供 Instance 静态单例
    //   3. spike Convert 增加类型容错（value 非 int 时返回 0.0）
    //
    // spike 简化：
    //   - 实现 Avalonia.Data.Converters.IValueConverter
    //   - Convert：int level → (level - 1) * 10.0（缩进像素值）
    //   - ConvertBack：throw NotSupportedException
    //   - Instance 静态单例
    public class LevelToIndentationConverter : IValueConverter
    {
        // 对照 WPF: private static readonly double Offset = 10.0
        private static readonly double Offset = 10.0;

        // spike 新增：Avalonia 转换器常用静态单例模式
        public static readonly LevelToIndentationConverter Instance = new LevelToIndentationConverter();

        // 对照 WPF: public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        //   double num = 0.0;
        //   if (value is int) num = (double)((int)value - 1) * Offset;
        //   return num;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double num = 0.0;
            if (value is int i)
            {
                num = (double)(i - 1) * Offset;
            }
            return num;
        }

        // 对照 WPF: public object ConvertBack(...) → throw new NotSupportedException()
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
