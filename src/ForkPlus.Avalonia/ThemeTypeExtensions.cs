using System;
using ForkPlus.UI;

// Avalonia spike 版 ThemeTypeExtensions（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/ThemeTypeExtensions.cs（22 行）：
//   - WPF: public static class ThemeTypeExtensionsWpf（故意改名避免与 Core 同名）
//   - ResourceUri(this ThemeType) 返回 pack://application URI
//     （pack://application:,,,/ForkPlus;component/Theme/Generic.{SkinName}.xaml）
//   - 纯逻辑方法（SkinName/IsDarkBase/AllThemes/SolidColorThemes/IsSolidColor）已迁到
//     ForkPlus.Core/UI/ThemeTypeExtensions.cs
//
// Avalonia 版差异：
//   1. WPF pack URI → Avalonia avares:// URI
//      （avares://ForkPlus.Avalonia/Themes/Brushes/Colors.{SkinName}.axaml，与 AvaloniaThemeService 一致）
//   2. 类名 ThemeTypeExtensionsWpf → ThemeTypeExtensionsAvalonia
//      （故意不取 ThemeTypeExtensions：Core 的 ForkPlus.UI.ThemeTypeExtensions 同名，
//       而 ToolbarUserControl 等文件按类型名引用 ThemeTypeExtensions.AllThemes/SolidColorThemes；
//       本工程命名空间 ForkPlus.Avalonia 是它们的外层命名空间，同名会造成遮蔽/歧义 CS0104。
//       扩展方法只按 namespace + this 参数签名解析，改名不影响 theme.ResourceUri() 调用。）
//   3. Core 的 SkinName() 扩展经 ProjectReference 可直接调用（跨 assembly 扩展方法解析）
//
// spike 简化：仅提供 ResourceUri（avares://），其余扩展沿用 Core。
namespace ForkPlus.Avalonia
{
	public static class ThemeTypeExtensionsAvalonia
	{
		/// <summary>皮肤对应的资源字典 URI（Avalonia avares:// URI: Colors.{SkinName}.axaml）。
		/// WPF 用 pack://application URI 指向 Theme/Generic.{SkinName}.xaml。</summary>
		public static Uri ResourceUri(this ThemeType themeType)
		{
			return new Uri("avares://ForkPlus.Avalonia/Themes/Brushes/Colors." + themeType.SkinName() + ".axaml");
		}
	}
}
