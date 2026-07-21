using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.12a：Avalonia 版 AboutDialogWindow（完整迁移版，对照 WPF AboutWindow.xaml.cs）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/AboutWindow.xaml.cs（44 行）：
    //   - public partial class AboutWindow : ForkPlusDialogWindow
    //   - 构造函数：base.ShowLogo=false / base.ShowFooter=false / InitializeComponent() /
    //     Translate("About " + App.AppName) → base.Title / base.DialogTitle /
    //     VersionTextBlock.Text = Translate("Version {0}", App.Version) /
    //     CopyrightTextBlock.Text = Translate("Copyright © {0} Hebin", DateTime.Now.Year)
    //   - Hyperlink_RequestNavigate: e.Uri.OpenInBrowser()
    //   - LegalHyperlink_RequestNavigate: new LegalWindow().ShowDialog()
    //   - Translate: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //
    // Avalonia 版完整迁移差异：
    //   1. ShowFooter=false（与 WPF 一致，没有 Submit/Cancel 按钮）
    //   2. 集成 ServiceLocator.Localization（PreferencesLocalization.Translate → ServiceLocator.Localization.Translate）
    //   3. 用 Button + Click 替代 Hyperlink.RequestNavigate（Avalonia 11 跨平台更稳）
    //   4. LegalButton 点击弹出 LegalWindow（已在 Phase 4.4b 迁移）
    //   5. 版本号读取 AssemblyInformationalVersionAttribute（fallback 到 GetName().Version）
    public partial class AboutDialogWindow : ForkPlusDialogWindow
    {
        public AboutDialogWindow()
        {
            // 与 WPF 一致：ShowLogo=false / ShowFooter=false
            ShowFooter = false;

            InitializeComponent();

            // 对照 WPF: Translate("About " + App.AppName)
            string title = Translate("About ForkPlus");
            DialogTitle = title;
            Title = title;

            // 对照 WPF: VersionTextBlock.Text = string.Format(Translate("Version {0}"), App.Version)
            VersionTextBlock.Text = string.Format(Translate("Version {0}"), GetVersion());

            // 对照 WPF: CopyrightTextBlock.Text = string.Format(Translate("Copyright © {0} Hebin"), DateTime.Now.Year)
            CopyrightTextBlock.Text = string.Format(Translate("Copyright © {0} Hebin"), DateTime.Now.Year);

            // 对照 WPF: LegalHyperlink → new LegalWindow().ShowDialog()
            LegalButton.Content = Translate("Legal");
        }

        // 对照 WPF: private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        private void AuthorLinkButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                OpenUrl(url);
            }
        }

        // 对照 WPF: private void LegalHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        //   → new LegalWindow().ShowDialog()
        private async void LegalButton_Click(object? sender, RoutedEventArgs e)
        {
            var legalWindow = new LegalWindow();
            await legalWindow.ShowDialog(this);
        }

        // 对照 WPF: private static string Translate(string text)
        //   return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
        // Phase 0.4b 完成后，ILocalizationService 已注册到 ServiceLocator
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

        private static string GetVersion()
        {
            AssemblyInformationalVersionAttribute? informationalVersion =
                Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (informationalVersion != null && !string.IsNullOrEmpty(informationalVersion.InformationalVersion))
            {
                return informationalVersion.InformationalVersion;
            }
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                return version.ToString();
            }
            return "0.0.0.0";
        }

        // 跨平台打开 URL（与 Phase 1 AboutWindow.OpenUrl 一致）
        private static void OpenUrl(string url)
        {
            try
            {
                var psi = new ProcessStartInfo { UseShellExecute = true };
                if (OperatingSystem.IsWindows())
                {
                    psi.FileName = url;
                }
                else if (OperatingSystem.IsMacOS())
                {
                    psi.FileName = "open";
                    psi.Arguments = $"\"{url}\"";
                }
                else
                {
                    psi.FileName = "xdg-open";
                    psi.Arguments = $"\"{url}\"";
                }
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open URL '{url}': {ex.Message}");
            }
        }
    }
}
