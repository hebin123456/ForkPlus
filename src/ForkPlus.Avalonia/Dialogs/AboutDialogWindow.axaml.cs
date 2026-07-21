using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.12a：Avalonia 版 AboutDialogWindow（第一个对话框 spike，验证 ForkPlusDialogWindow 基类）。
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
    // Avalonia 版差异（spike 简化策略）：
    //   1. spike 版 ShowFooter=false（与 WPF 一致，没有 Submit/Cancel 按钮）
    //   2. spike 版不调 SetFooter（ShowFooter=false 时基类 OnCancel 不会被 ESC 触发）
    //   3. spike 版不接入 PreferencesLocalization.Translate（直接用字面量英文）
    //   4. spike 版用 Button + Click 替代 Hyperlink.RequestNavigate
    //   5. spike 版不弹 LegalWindow（Phase 4.12b 再迁）
    //   6. 版本号读取：与 Phase 1 AboutWindow 一致，从 AssemblyInformationalVersionAttribute
    //
    // 本 spike 版验证：
    //   - ForkPlusDialogWindow 基类继承链工作（CustomWindow → Window + 基类 API 可用）
    //   - 子类设置 DialogTitle 同步 Window.Title
    //   - ShowFooter=false 时窗口无 Footer 也能正确显示
    public partial class AboutDialogWindow : ForkPlusDialogWindow
    {
        public AboutDialogWindow()
        {
            // 与 WPF 一致：ShowLogo=false / ShowFooter=false
            ShowFooter = false;

            InitializeComponent();

            // 对照 WPF: Translate("About " + App.AppName)
            DialogTitle = "About ForkPlus";
            Title = "About ForkPlus";

            // 对照 WPF: VersionTextBlock.Text = string.Format(Translate("Version {0}"), App.Version)
            VersionTextBlock.Text = string.Format("Version {0}", GetVersion());

            // 对照 WPF: CopyrightTextBlock.Text = string.Format(Translate("Copyright © {0} Hebin"), DateTime.Now.Year)
            CopyrightTextBlock.Text = string.Format("Copyright © {0} Hebin", DateTime.Now.Year);
        }

        // 对照 WPF: private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        // spike 版用 Button.Click + Tag=URL
        private void AuthorLinkButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
            {
                OpenUrl(url);
            }
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
