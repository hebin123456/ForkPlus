using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ResizeModeToVisibilityConverter（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ResizeModeToVisibilityConverter.cs（19 行）：
    //   - WPF ResizeModeToVisibilityConverter : IValueConverter
    //   - Convert(object value) → Visibility
    //     return ((ResizeMode)value != ResizeMode.CanResizeWithGrip) ? Visibility.Collapsed : Visibility.Visible
    //   - ConvertBack → throw NotSupportedException
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IValueConverter → Avalonia.Data.Converters.IValueConverter
    //   2. WPF System.Windows.ResizeMode 枚举 → Avalonia 无对应枚举
    //      spike 用 int 接收 + 字符串比较（"CanResizeWithGrip"）
    //      或 int 数值比较（WPF ResizeMode.CanResizeWithGrip = 3，枚举第 4 个值）
    //   3. WPF Visibility.Collapsed / Visibility.Visible → spike 返回 bool
    //      （Avalonia 用 IsVisible = false / true 替代 WPF Visibility.Collapsed / Visible）
    //   4. spike 提供 Instance 静态单例
    //   5. spike Convert 增加 null/类型容错（value 非 int/string 时返回 false）
    //
    // spike 简化：
    //   - 实现 Avalonia.Data.Converters.IValueConverter
    //   - Convert：value==CanResizeWithGrip → true（Visible）/ 其他 → false（Collapsed）
    //   - ConvertBack：throw NotSupportedException
    //   - Instance 静态单例
    public class ResizeModeToVisibilityConverter : IValueConverter
    {
        // spike 新增：Avalonia 转换器常用静态单例模式
        public static readonly ResizeModeToVisibilityConverter Instance = new ResizeModeToVisibilityConverter();

        // 对照 WPF: ((ResizeMode)value != ResizeMode.CanResizeWithGrip) ? Visibility.Collapsed : Visibility.Visible
        // spike 版：
        //   - int value：3 == CanResizeWithGrip → true（Visible）
        //   - string value："CanResizeWithGrip" → true
        //   - 其他 → false（Collapsed）
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
            {
                // WPF ResizeMode 枚举：NoResize=0, CanMinimize=1, CanResize=2, CanResizeWithGrip=3
                return i == 3;
            }
            if (value is string s)
            {
                return s == "CanResizeWithGrip";
            }
            return false;
        }

        // 对照 WPF: public object ConvertBack(...) → throw new NotSupportedException()
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
