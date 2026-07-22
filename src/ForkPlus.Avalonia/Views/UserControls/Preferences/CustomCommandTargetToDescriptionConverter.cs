// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/CustomCommandTargetToDescriptionConverter.cs（42 行）：
//   - public class CustomCommandTargetToDescriptionConverter : MarkupExtension, IValueConverter
//   - Convert：CustomCommandTarget 枚举 → 描述字符串（Commit/Repository/File/Branch/Submodule）
//   - ConvertBack：throw NotImplementedException
//   - ProvideValue：返回 this（MarkupExtension 模式，XAML 中直接引用）
//
// Avalonia 版差异：
//   1. WPF MarkupExtension → Avalonia 无 MarkupExtension 基类（Avalonia 用 {x:Static} 或
//      直接在 XAML 中实例化 converter）。spike 版移除 MarkupExtension 继承，
//      仅实现 Avalonia.Data.Converters.IValueConverter。
//   2. WPF System.Windows.Data.IValueConverter → Avalonia.Data.Converters.IValueConverter
//      （签名一致：Convert(object, Type, object, CultureInfo) / ConvertBack）
//   3. WPF CultureInfo → System.Globalization.CultureInfo（不变）
//   4. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public class CustomCommandTargetToDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CustomCommandTarget)
            {
                switch ((CustomCommandTarget)value)
                {
                    case CustomCommandTarget.Revision:
                        return "Commit";
                    case CustomCommandTarget.Repository:
                        return "Repository";
                    case CustomCommandTarget.RepositoryFile:
                        return "File";
                    case CustomCommandTarget.Reference:
                        return "Branch";
                    case CustomCommandTarget.Submodule:
                        return "Submodule";
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
