using System;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Services
{
    // Phase 2.1：主题服务接口。
    // 抽象主题切换操作，让 ViewModel / Settings 不直接依赖 Avalonia 平台 API。
    //
    // 对照 WPF 工程 src/ForkPlus/App.xaml.cs 的 InitializeTheme / ApplyCustomColors 方法：
    //   - WPF 用 Application.Current.Resources.MergedDictionaries 切换 Generic.{Skin}.xaml
    //   - Avalonia 用 Application.Current.Resources.MergedDictionaries 切换 .axaml
    //     或用 RequestedThemeVariant 切换 Fluent 内置 Light/Dark
    //
    // Phase 2.1 阶段：仅实现 Fluent 兜底（Light/Dark 二元切换），
    // 22 套主题变体的完整迁移在 Phase 2.2-2.4 完成。
    public interface IThemeService
    {
        /// <summary>当前应用的主题。</summary>
        ThemeType CurrentTheme { get; }

        /// <summary>应用指定主题。
        /// Phase 2.1：仅映射到 Fluent ThemeVariant.Light/Dark（按 IsDarkBase 二元切换）。
        /// Phase 2.2-2.4：完整支持 22 套主题变体（加载对应 .axaml 资源字典）。</summary>
        void ApplyTheme(ThemeType theme);

        /// <summary>事件：主题切换后触发（用于 ViewModel 刷新绑定）。</summary>
        event EventHandler<ThemeType> ThemeChanged;
    }
}
