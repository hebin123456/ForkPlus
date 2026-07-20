using System;
using Avalonia;
using Avalonia.Styling;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Services
{
    // Phase 2.1：IThemeService 的 Avalonia 实现。
    // 当前阶段仅用 Fluent ThemeVariant.Light/Dark 二元兜底，22 套主题变体的
    // 完整资源字典迁移在 Phase 2.2-2.4 完成。
    //
    // 对照 WPF 工程 src/ForkPlus/App.xaml.cs InitializeTheme():
    //   WPF: Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
    //            Source = theme.ResourceUri()  // pack://application:,,,/ForkPlus;component/Theme/Generic.{Skin}.xaml
    //        });
    //   Avalonia Phase 2.1: Application.Current.RequestedThemeVariant = ThemeVariant.Light/Dark;
    //   Avalonia Phase 2.2+: Application.Current.Resources.MergedDictionaries 切换
    //                        avares://ForkPlus.Avalonia/Themes/Generic.{Skin}.axaml
    public class AvaloniaThemeService : IThemeService
    {
        public ThemeType CurrentTheme { get; private set; } = ThemeType.Light;

        public event EventHandler<ThemeType> ThemeChanged;

        public void ApplyTheme(ThemeType theme)
        {
            CurrentTheme = theme;
            // Phase 2.1：按 IsDarkBase 二元映射到 Fluent ThemeVariant
            // Phase 2.2-2.4 会改为加载完整主题资源字典
            Application.Current.RequestedThemeVariant = theme.IsDarkBase()
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

            ThemeChanged?.Invoke(this, theme);
        }
    }
}
