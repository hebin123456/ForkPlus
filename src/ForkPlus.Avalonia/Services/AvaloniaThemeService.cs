using System;
using Avalonia;
using Avalonia.Styling;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Services
{
    // Phase 2.1/2.5：IThemeService 的 Avalonia 实现。
    //
    // 对照 WPF 工程 src/ForkPlus/App.xaml.cs InitializeTheme():
    //   WPF: Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {
    //            Source = theme.ResourceUri()  // pack://application:,,,/ForkPlus;component/Theme/Generic.{Skin}.xaml
    //        });
    //
    // Avalonia 11 实现：
    //   1. 切换 Fluent ThemeVariant（Light/Dark 二元兜底，让 Fluent 自带控件主题适配）
    //   2. 加载对应 Colors.{Skin}.axaml 资源字典（289 个 Color 资源）
    //   3. 加载共享 Brushes.axaml（SolidColorBrush 引用 Color 资源）
    //
    // Phase 2.5 新增：完整 22 套主题资源字典加载（之前 Phase 2.1 只做 Fluent 二元兜底）。
    public class AvaloniaThemeService : IThemeService
    {
        public ThemeType CurrentTheme { get; private set; } = ThemeType.Light;

        public event EventHandler<ThemeType> ThemeChanged;

        public void ApplyTheme(ThemeType theme)
        {
            CurrentTheme = theme;

            // 1. 切换 Fluent ThemeVariant（让 Fluent 自带控件主题适配 Light/Dark）
            Application.Current.RequestedThemeVariant = theme.IsDarkBase()
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

            // 2. 加载对应主题资源字典（Colors + Brushes）
            LoadThemeResources(theme);

            ThemeChanged?.Invoke(this, theme);
        }

        /// <summary>加载指定主题的 Colors + Brushes 资源字典。
        /// Phase 2.5：22 套主题的 Colors.*.axaml 已迁移，加共享 Brushes.axaml。</summary>
        private static void LoadThemeResources(ThemeType theme)
        {
            var app = Application.Current;
            if (app == null) return;

            string skinName = theme.SkinName();
            string colorsUri = $"avares://ForkPlus.Avalonia/Themes/Brushes/Colors.{skinName}.axaml";
            string brushesUri = "avares://ForkPlus.Avalonia/Themes/Brushes/Brushes.axaml";

            try
            {
                // 加载 Colors 资源字典
                var colorsRd = new ResourceDictionary
                {
                    Source = new Uri(colorsUri)
                };

                // 加载共享 Brushes 资源字典
                var brushesRd = new ResourceDictionary
                {
                    Source = new Uri(brushesUri)
                };

                // 移除已有的主题资源字典（避免重复加载）
                // 标记：用 [0] 是 Colors，[1] 是 Brushes（约定）
                var existing = new System.Collections.Generic.List<ResourceDictionary>();
                foreach (var rd in app.Resources.MergedDictionaries)
                {
                    if (rd.Source != null &&
                        (rd.Source.OriginalString.Contains("/Themes/Brushes/Colors.") ||
                         rd.Source.OriginalString.Contains("/Themes/Brushes/Brushes.axaml")))
                    {
                        existing.Add(rd);
                    }
                }
                foreach (var rd in existing)
                {
                    app.Resources.MergedDictionaries.Remove(rd);
                }

                // 添加新主题（顺序：先 Colors 后 Brushes，让 Brushes 能引用 Colors）
                app.Resources.MergedDictionaries.Add(colorsRd);
                app.Resources.MergedDictionaries.Add(brushesRd);
            }
            catch (Exception ex)
            {
                // Phase 2.5：资源加载失败时降级到 Fluent 兜底（不崩溃）
                Console.WriteLine($"[AvaloniaThemeService] Failed to load theme {skinName}: {ex.Message}");
                Console.WriteLine("[AvaloniaThemeService] Falling back to Fluent default theme");
            }
        }
    }
}
