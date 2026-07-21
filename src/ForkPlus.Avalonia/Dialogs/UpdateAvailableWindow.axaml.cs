using System;
using Avalonia.Controls;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.17b：Avalonia 版 UpdateAvailableWindow（真实迁移版，对照 WPF UpdateAvailableWindow.xaml.cs 56 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/UpdateAvailableWindow.xaml.cs：
    //   - public partial class UpdateAvailableWindow : ForkPlusDialogWindow
    //   - private readonly UpdateInfo _updateInfo
    //   - 构造函数 (UpdateInfo updateInfo):
    //     * DialogTitle = PreferencesLocalization.Current("Update Available")
    //     * DialogDescription = PreferencesLocalization.FormatCurrent(
    //         "A new version {0} is available (current: {1}).",
    //         updateInfo.LatestVersion, updateInfo.CurrentVersion)
    //     * SubmitButtonTitle = PreferencesLocalization.Current("Download")
    //     * CancelButtonTitle = PreferencesLocalization.Current("Later")
    //     * ReleaseNotesTextBox.Text = releaseNotes ?? releaseName
    //   - OnSubmit: new Uri(downloadUrl).OpenInBrowser();
    //               if (SkipVersionCheckBox.IsChecked == true) UpdateChecker.SkipVersion(latest);
    //               base.OnSubmit()
    //   - OnCancel: if (SkipVersionCheckBox.IsChecked == true) UpdateChecker.SkipVersion(latest);
    //               base.OnCancel()
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Current/FormatCurrent → ServiceLocator.Localization.Current/FormatCurrent
    //   3. UpdateChecker/UpdateInfo 已在 Phase 4.17b 移到 Core（原 WPF src/ForkPlus/UpdateChecker.cs 删除）
    //   4. UriExtensions.OpenInBrowser 已改为 public（原 internal），Core 可直接调用
    public partial class UpdateAvailableWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly UpdateInfo _updateInfo;

        public UpdateAvailableWindow(UpdateInfo updateInfo)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _updateInfo = updateInfo ?? throw new ArgumentNullException(nameof(updateInfo));

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle / CancelButtonTitle
            string title = Translate("Update Available");
            Title = title;
            DialogTitle = title;
            DialogDescription = FormatTranslate(
                "A new version {0} is available (current: {1}).",
                updateInfo.LatestVersion, updateInfo.CurrentVersion);
            SubmitButtonTitle = Translate("Download");
            CancelButtonTitle = Translate("Later");

            // 对照 WPF: ReleaseNotesTextBox.Text = string.IsNullOrEmpty(updateInfo.ReleaseNotes)
            //   ? updateInfo.ReleaseName : updateInfo.ReleaseNotes;
            ReleaseNotesTextBox.Text = string.IsNullOrEmpty(updateInfo.ReleaseNotes)
                ? updateInfo.ReleaseName
                : updateInfo.ReleaseNotes;
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            try
            {
                if (!string.IsNullOrEmpty(_updateInfo.DownloadUrl))
                {
                    new Uri(_updateInfo.DownloadUrl).OpenInBrowser();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to open download url", ex);
            }
            if (SkipVersionCheckBox.IsChecked == true)
            {
                UpdateChecker.SkipVersion(_updateInfo.LatestVersion);
            }
            CloseWithOk();
        }

        // 对照 WPF: protected override void OnCancel()
        protected override void OnCancel()
        {
            if (SkipVersionCheckBox.IsChecked == true)
            {
                UpdateChecker.SkipVersion(_updateInfo.LatestVersion);
            }
            base.OnCancel();
        }

        // 对照 WPF: PreferencesLocalization.Current(text)
        // Phase 0.4b 完成后，使用 ServiceLocator.Localization
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

        // 对照 WPF: PreferencesLocalization.FormatCurrent(text, args)
        private static string FormatTranslate(string text, params object[] args)
        {
            var localization = ServiceLocator.Localization;
            var userSettings = ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.FormatCurrent(text, args);
            }
            return string.Format(text, args);
        }
    }
}
