// HeaderCommandProviderItem.cs：分组标题条目（POCO）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/HeaderCommandProviderItem.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class HeaderCommandProviderItem : CommandProviderItem
//   - 构造函数：base(name, PreferencesLocalization.Translate(name, ForkPlusSettings.Default.UiLanguage), "")
//
// Avalonia 版差异：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. PreferencesLocalization.Translate(name, ForkPlusSettings.Default.UiLanguage)
//      → ServiceLocator.Localization.Translate(name, ServiceLocator.UserSettings.UiLanguage)
//      （task spec 关键 API：PreferencesLocalization → ServiceLocator.Localization）

using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class HeaderCommandProviderItem : CommandProviderItem
    {
        public HeaderCommandProviderItem(string name)
            : base(name, Translate(name), "")
        {
        }

        // 对照 WPF: PreferencesLocalization.Translate(name, ForkPlusSettings.Default.UiLanguage)
        // spike: ServiceLocator.Localization.Translate(name, ServiceLocator.UserSettings.UiLanguage)
        private static string Translate(string name)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(name, userSettings.UiLanguage);
            }
            return name;
        }
    }
}
