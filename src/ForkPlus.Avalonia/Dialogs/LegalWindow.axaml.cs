using System;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.4b：Avalonia 版 LegalWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/LegalWindow.xaml.cs（46 行）：
    //   - public partial class LegalWindow : ForkPlusDialogWindow
    //   - 构造函数：
    //     * ShowLogo=false, ShowHeader=false
    //     * InitializeComponent()
    //     * CancelButtonTitle = PreferencesLocalization.Current("Close")
    //     * ShowSubmitButton = false
    //     * LicencesTextBox.Text = GetLicences()
    //   - GetLicences():
    //     * 读嵌入资源 "ForkPlus.Assets.Legal.txt" → 全文返回
    //     * 异常时 Log.Error 并返回空字符串
    //
    // 调用方（WPF 版）：
    //   new LegalWindow().ShowDialog()
    //
    // 调用方（Avalonia 版）：
    //   await new LegalWindow().ShowDialog(owner)
    public partial class LegalWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        public LegalWindow()
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);

            // 对照 WPF: base.CancelButtonTitle = PreferencesLocalization.Current("Close")
            CancelButtonTitle = Current("Close");
            // 对照 WPF: base.ShowSubmitButton = false
            ShowSubmitButton = false;

            // 对照 WPF: LicencesTextBox.Text = GetLicences()
            LicencesTextBox.Text = GetLicences();
        }

        // 对照 WPF: private static string GetLicences()
        private static string GetLicences()
        {
            string result = string.Empty;
            try
            {
                Assembly executingAssembly = Assembly.GetExecutingAssembly();
                const string name = "ForkPlus.Assets.Legal.txt";
                using Stream stream = executingAssembly.GetManifestResourceStream(name);
                if (stream == null)
                {
                    Log.Error($"Embedded resource '{name}' not found in assembly {executingAssembly.GetName().Name}");
                    return result;
                }
                using StreamReader streamReader = new StreamReader(stream);
                result = streamReader.ReadToEnd();
                return result;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read resource stream", ex);
                return result;
            }
        }

        // PreferencesLocalization.Current(text) → ServiceLocator.Localization.Current(text)
        private static string Current(string text)
        {
            var localization = ServiceLocator.Localization;
            return localization != null ? localization.Current(text) : text;
        }
    }
}
