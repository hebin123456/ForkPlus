using System;
using Avalonia.Media;
// Avalonia spike 版 SystemThemeHelper（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/SystemThemeHelper.cs（74 行）：
//   - WPF: internal static class SystemThemeHelper
//   - WinRT-only：依赖 Windows.UI.ViewManagement.UISettings（Win10+）
//   - SubscribeToSystemEvents()：订阅 UISettings.ColorValuesChanged
//   - GetSystemBrush(Theme.SystemColorType)：从 UISettings.GetColorValue 获取系统强调色
//   - UiSettings_ColorValuesChanged：Theme.Refresh()
//   - 依赖：Windows.Foundation / Windows.UI / Windows.UI.ViewManagement（UWP WinRT）
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF WinRT UISettings → Avalonia 无 WinRT 依赖（跨平台）
//      spike 版：完全跳过系统主题订阅（spike 不实现系统颜色变化监听）
//   2. WPF SolidColorBrush + Freeze() → Avalonia ISolidColorBrush（无需 Freeze）
//   3. WPF Application.Current.Dispatcher?.Invoke → Avalonia Dispatcher.UIThread.Post
//   4. WPF Theme.SystemColorType / Theme.Refresh() → spike 跳过（Theme 类在 Group 7）
//
// spike 简化（task spec 关键 API）：
//   - SubscribeToSystemEvents()（spike 空实现，跨平台无 WinRT）
//   - GetSystemBrush()（spike 返回 null 占位）
namespace ForkPlus.Avalonia
{
    // spike 版：放在 ForkPlus.Avalonia 命名空间（task spec：Manager 类用此命名空间）。
    internal static class SystemThemeHelper
    {
        // 对照 WPF: public static void SubscribeToSystemEvents()
        //   UISettings uiSettings = new UISettings();
        //   uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
        // spike 版：空实现（跨平台无 WinRT UISettings）
        public static void SubscribeToSystemEvents()
        {
            // spike 版跳过：Avalonia 无 WinRT 依赖，系统颜色变化监听留待 Phase 4.x
            // 可用 Avalonia Application.ActualThemeVariantChanged 替代
        }

        // 对照 WPF: public static Brush GetSystemBrush(Theme.SystemColorType colorType)
        //   return new SolidColorBrush(GetColor(((UISettings)_uiSettings).GetColorValue(...)));
        // spike 版：返回 null 占位（spike 不读系统强调色）
        public static IBrush GetSystemBrush(string colorType)
        {
            // spike 版跳过：无 UISettings 依赖，返回 null
            return null;
        }
    }
}
