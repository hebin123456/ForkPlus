using System.ComponentModel;
using ForkPlus.Services;
using ForkPlus.Utils.Http;

namespace ForkPlus.Avalonia.Dialogs.Accounts
{
    // Phase 4.21b：Avalonia 版 AuthenticationItem（对照 WPF AuthenticationItem.cs 22 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/Accounts/AuthenticationItem.cs：
    //   - public class AuthenticationItem : INotifyPropertyChanged
    //   - 属性: AuthenticationType Type / string Title
    //   - 构造函数 (AuthenticationType type, string title):
    //     * Title = PreferencesLocalization.Translate(title, ForkPlusSettings.Default.UiLanguage)
    //   - INPC 用于 ComboBox 选中态绑定（WPF Binding 模式）
    //
    // Avalonia 版差异：
    //   1. PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //      → ServiceLocator.Localization.Translate(text, userSettings.UiLanguage)
    //   2. INotifyPropertyChanged 直接保留（Avalonia 数据绑定同 WPF）
    public class AuthenticationItem : INotifyPropertyChanged
    {
        public AuthenticationType Type { get; }

        public string Title { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public AuthenticationItem(AuthenticationType type, string title)
        {
            Type = type;
            Title = Translate(title);
        }

        private static string Translate(string text)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(text, userSettings.UiLanguage);
            }
            return text;
        }
    }
}
