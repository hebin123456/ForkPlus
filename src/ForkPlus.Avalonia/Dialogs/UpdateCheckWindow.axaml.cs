using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.18b：Avalonia 版 UpdateCheckWindow（真实迁移版，对照 WPF UpdateCheckWindow.xaml.cs 161 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/UpdateCheckWindow.xaml.cs：
    //   - public partial class UpdateCheckWindow : ForkPlusDialogWindow
    //   - private readonly UpdateChecker _checker = new UpdateChecker();
    //   - private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    //   - private UpdateInfo _result;
    //   - 构造函数:
    //     * DialogTitle = PreferencesLocalization.Current("Check for Updates")
    //     * DialogDescription = PreferencesLocalization.Current("Checking for updates...")
    //     * SubmitButtonTitle = PreferencesLocalization.Current("Download")
    //     * CancelButtonTitle = PreferencesLocalization.Current("Close")
    //     * ShowSubmitButton = false  （检测完成前隐藏）
    //     * Loaded += UpdateCheckWindow_Loaded
    //   - StartCheck: Task.Run(_checker.CheckLatestRelease) → Dispatcher.Invoke(OnCheckCompleted)
    //   - OnCheckCompleted: 切换 CheckingPanel/ResultPanel 可见性
    //   - OnSubmit: new Uri(downloadUrl).OpenInBrowser(); if (SkipVersion) UpdateChecker.SkipVersion
    //   - OnCancel: _cts.Cancel(); if (SkipVersion && HasUpdate) UpdateChecker.SkipVersion
    //   - OnClosed: _cts.Cancel(); _cts.Dispose()
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Current/FormatCurrent → ServiceLocator.Localization.Translate/FormatCurrent
    //   3. Dispatcher.Invoke → Dispatcher.UIThread.Post
    //   4. Visibility.Collapsed/Visible → IsVisible = false/true
    //   5. Loaded += → Loaded += （Avalonia 同名事件）
    //   6. OnClosed override → Closed += （Avalonia 事件，不是 override）
    //   7. UpdateChecker/UpdateInfo 已在 Core（Phase 4.17b）
    //   8. base.OnSubmit() → CloseWithOk()（spike 版基类 OnSubmit 即 CloseWithOk，但显式调用更清晰）
    public partial class UpdateCheckWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly UpdateChecker _checker = new UpdateChecker();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private UpdateInfo _result;

        public UpdateCheckWindow()
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: DialogTitle / DialogDescription / SubmitButtonTitle / CancelButtonTitle
            string title = Translate("Check for Updates");
            Title = title;
            DialogTitle = title;
            DialogDescription = Translate("Checking for updates...");
            SubmitButtonTitle = Translate("Download");
            CancelButtonTitle = Translate("Close");
            // 检测完成前隐藏 Submit（下载）按钮
            ShowSubmitButton = false;

            // 对照 WPF: Loaded += UpdateCheckWindow_Loaded;
            Loaded += UpdateCheckWindow_Loaded;
            // 对照 WPF: protected override void OnClosed(EventArgs e)
            Closed += UpdateCheckWindow_Closed;
        }

        private void UpdateCheckWindow_Loaded(object? sender, EventArgs e)
        {
            StartCheck();
        }

        private void UpdateCheckWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            catch
            {
            }
        }

        private void StartCheck()
        {
            CancellationToken token = _cts.Token;
            Task.Run(delegate
            {
                UpdateInfo? info = null;
                try
                {
                    info = _checker.CheckLatestRelease(token);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    UpdateChecker.MarkChecked();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    info = new UpdateInfo { ErrorMessage = ex.Message };
                    Log.Warn("Update check outer exception: " + ex.Message);
                }
                if (token.IsCancellationRequested)
                {
                    return;
                }
                // 对照 WPF: Dispatcher.Invoke(new Action(() => OnCheckCompleted(info)));
                // Avalonia 11: Dispatcher.UIThread.Post
                Dispatcher.UIThread.Post(() => OnCheckCompleted(info));
            }, token);
        }

        private void OnCheckCompleted(UpdateInfo? info)
        {
            _result = info;
            CheckingPanel.IsVisible = false;
            ResultPanel.IsVisible = true;
            if (info == null || (!string.IsNullOrEmpty(info.ErrorMessage) && info.ErrorMessage != "Cancelled"))
            {
                // 检测失败
                string err = info?.ErrorMessage ?? "Unknown error";
                VersionInfoTextBlock.Text = FormatTranslate("Update check failed: {0}", err);
                ReleaseNotesLabel.IsVisible = false;
                ReleaseNotesTextBox.IsVisible = false;
                SkipVersionCheckBox.IsVisible = false;
                ShowSubmitButton = false;
                StatusTextBlock.Text = "";
                return;
            }
            if (info.HasUpdate)
            {
                // 有更新
                VersionInfoTextBlock.Text = FormatTranslate(
                    "A new version {0} is available (current: {1}).",
                    info.LatestVersion, info.CurrentVersion);
                ReleaseNotesTextBox.Text = string.IsNullOrEmpty(info.ReleaseNotes)
                    ? info.ReleaseName
                    : info.ReleaseNotes;
                ShowSubmitButton = true;
            }
            else
            {
                // 已是最新（附当前版本号）
                VersionInfoTextBlock.Text = FormatTranslate(
                    "You are using the latest version (v{0}).", info.CurrentVersion);
                ReleaseNotesLabel.IsVisible = false;
                ReleaseNotesTextBox.IsVisible = false;
                SkipVersionCheckBox.IsVisible = false;
                ShowSubmitButton = false;
            }
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            try
            {
                if (_result != null && !string.IsNullOrEmpty(_result.DownloadUrl))
                {
                    new Uri(_result.DownloadUrl).OpenInBrowser();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to open download url", ex);
            }
            if (SkipVersionCheckBox.IsChecked == true && _result != null)
            {
                UpdateChecker.SkipVersion(_result.LatestVersion);
            }
            CloseWithOk();
        }

        // 对照 WPF: protected override void OnCancel()
        protected override void OnCancel()
        {
            // 关闭窗口：立即取消正在进行的检测
            try
            {
                _cts.Cancel();
            }
            catch
            {
            }
            if (SkipVersionCheckBox.IsChecked == true && _result != null && _result.HasUpdate)
            {
                UpdateChecker.SkipVersion(_result.LatestVersion);
            }
            base.OnCancel();
        }

        // 对照 WPF: PreferencesLocalization.Current(text)
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
