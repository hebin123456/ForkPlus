using System;
using ForkPlus.UI;

namespace ForkPlus.UI
{
    // Phase 2.1：WPF 工程专用的 ThemeType 扩展（仅保留 WPF-specific 的 ResourceUri）。
    // 纯逻辑方法（SkinName/IsDarkBase/AllThemes/SolidColorThemes/IsSolidColor）已迁到
    // ForkPlus.Core/UI/ThemeTypeExtensions.cs，本工程通过 ProjectReference 引用 Core
    // 后仍能调用这些扩展方法（C# 扩展方法解析跨 assembly）。
    //
    // 类名故意取为 ThemeTypeExtensionsWpf（而非 ThemeTypeExtensions），避免与 Core 的
    // ThemeTypeExtensions 同名——虽然 C# 允许同名 static class 跨 assembly 存在，
    // 但用不同类名更清晰，调用方代码无需修改（扩展方法解析只看 namespace + this 参数签名）。
    public static class ThemeTypeExtensionsWpf
    {
        /// <summary>皮肤对应的资源字典 URI（WPF pack URI: Generic.{SkinName}.xaml）。
        /// Avalonia 工程有自己的 ResourceUri 扩展（avares:// URI）。</summary>
        public static Uri ResourceUri(this ThemeType themeType)
        {
            return new Uri("pack://application:,,,/ForkPlus;component/Theme/Generic." + themeType.SkinName() + ".xaml");
        }
    }
}
