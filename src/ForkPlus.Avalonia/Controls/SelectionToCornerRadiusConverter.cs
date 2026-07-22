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
    //   - 命名空间 ForkPlus.UI（注意：WPF 源在 UI/ 目录而非 UI/Controls/）
    //   - 4 个静态 CornerRadius 字段：None/All/Top/Bottom
    //   - Convert(object value) → CornerRadius
    //     ListBoxSelectionType.None/Middle → None(0,0,0,0)
    //     ListBoxSelectionType.Top → Top(4,4,0,0)
    //     ListBoxSelectionType.Bottom → Bottom(0,0,4,4)
    //     非 ListBoxSelectionType → All(4,4,4,4)
    //   - ConvertBack → throw NotImplementedException
    //   - ProvideValue(IServiceProvider) → this（MarkupExtension 模式）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF System.Windows.Data.IValueConverter → Avalonia.Data.Converters.IValueConverter
    //   2. WPF System.Windows.CornerRadius → Avalonia.CornerRadius
    //   3. WPF System.Windows.Markup.MarkupExtension → spike 跳过（Avalonia 转换器常用 Instance 单例模式）
    //   4. spike 提供 Instance 静态单例
    //   5. spike Convert 增加 null/类型容错
    //
    // spike 简化：
    //   - 实现 Avalonia.Data.Converters.IValueConverter
    //   - Convert：ListBoxSelectionType → CornerRadius
    //   - ConvertBack：throw NotSupportedException（统一 spike 异常策略）
    //   - Instance 静态单例
    public class SelectionToCornerRadiusConverter : IValueConverter
    {
        // 对照 WPF: private static CornerRadius CornerRadiusNone = new CornerRadius(0.0, 0.0, 0.0, 0.0)
        private static readonly CornerRadius CornerRadiusNone = new CornerRadius(0.0, 0.0, 0.0, 0.0);

        // 对照 WPF: private static CornerRadius CornerRadiusAll = new CornerRadius(4.0, 4.0, 4.0, 4.0)
        private static readonly CornerRadius CornerRadiusAll = new CornerRadius(4.0, 4.0, 4.0, 4.0);

        // 对照 WPF: private static CornerRadius CornerRadiusTop = new CornerRadius(4.0, 4.0, 0.0, 0.0)
        private static readonly CornerRadius CornerRadiusTop = new CornerRadius(4.0, 4.0, 0.0, 0.0);

        // 对照 WPF: private static CornerRadius CornerRadiusBottom = new CornerRadius(0.0, 0.0, 4.0, 4.0)
        private static readonly CornerRadius CornerRadiusBottom = new CornerRadius(0.0, 0.0, 4.0, 4.0);

        // spike 新增：Avalonia 转换器常用静态单例模式（替代 WPF MarkupExtension.ProvideValue）
        public static readonly SelectionToCornerRadiusConverter Instance = new SelectionToCornerRadiusConverter();

        // 对照 WPF: public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        //   if (!(value is ListBoxSelectionType)) return CornerRadiusAll;
        //   return (ListBoxSelectionType)value switch { ... }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ListBoxSelectionType))
            {
                return Cor