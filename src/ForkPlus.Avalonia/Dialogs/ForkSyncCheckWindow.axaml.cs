using System;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.20b：Avalonia 版 ForkSyncCheckWindow（真实迁移版，对照 WPF ForkSyncCheckWindow.xaml.cs 138 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ForkSyncCheckWindow.xaml.cs：
    //   - public partial class ForkSyncCheckWindow : ForkPlusDialogWindow
    //   - 字段: RepositoryUserControl _repositoryUserControl / Remote _upstreamRemote /
    //           LocalBranch _localBranch / string _branchName / ForkSyncStatus? _status
    //   - 构造函数 (RepositoryUserControl, Remote, LocalBranch, string branchName, ForkSyncStatus? status)
    //   - IsSubmitAllowed override: _status.HasValue && base.IsSubmitAllowed
    //   - UpdateResult(ForkSyncStatus): 刷新状态并 UpdateSubmitButton
    //   - ConfigureForStatus: 根据 status 切换 StatusIcon/StatusText/DetailText/按钮文案
    //   - OnSubmit: 根据 status 决定关闭或打开 Pull 窗口
    //     * SafeToPush/NoUpstreamBranch/Unknown → 直接关闭
    //     * ShouldSyncNoConflict / MustSyncWithConflict → RepositoryUserControl.Commands?.ShowPullWindow?.Execute(...)
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. PreferencesLocalization.Current/FormatCurrent → ServiceLocator.Localization.Translate/FormatCurrent
    //   3. RepositoryUserControl 参数 → Action? onPullRequested 回调（解耦，调用方注入）
    //      WPF OnSubmit: RepositoryUserControl.Commands?.ShowPullWindow?.Execute(_repositoryUserControl, null);
    //      Avalonia: 直接调用 onPullRequested?.Invoke()
    //   4. BitmapImage(SuccessIcon/WarningIcon/ErrorIcon) → emoji TextBlock（"✓"/"⚠"/"✗"）
    //      spike 版参考 ForkPlusDialogWindow.SetStatus 的做法，避免引入 PNG 资源
    public partial class ForkSyncCheckWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly Remote _upstreamRemote;
        private readonly LocalBranch _localBranch;
        private readonly string _branchName;
        private readonly Action? _onPullRequested;
        private ForkSyncStatus? _status;

        // 构造函数签名与 WPF 不同：用 Action? onPullRequested 替代 RepositoryUserControl
        // （RepositoryUserControl 在 Avalonia 端尚未迁移，spike 版解耦）
        public ForkSyncCheckWindow(
            Remote upstreamRemote,
            LocalBranch localBranch,
            string branchName,
            ForkSyncStatus? status,
            Action? onPullRequested = null)
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            _upstreamRemote = upstreamRemote ?? throw new ArgumentNullException(nameof(upstreamRemote));
            _localBranch = localBranch ?? throw new ArgumentNullException(nameof(localBranch));
            _branchName = branchName ?? "";
            _status = status;
            _onPullRequested = onPullRequested;

            // 对照 WPF: DialogTitle = PreferencesLocalization.Current("Remote Sync Status");
            //          DialogDescription = string.Empty;
            string title = Translate("Remote Sync Status");
            Title = title;
            DialogTitle = title;
            DialogDescription = "";

            ConfigureForStatus();
        }

        // 对照 WPF: protected override bool IsSubmitAllowed => _status.HasValue && base.IsSubmitAllowed;
        protected override bool IsSubmitAllowed => _status.HasValue && base.IsSubmitAllowed;

        // 对照 WPF: public void UpdateResult(ForkSyncStatus status)
        public void UpdateResult(ForkSyncStatus status)
        {
            _status = status;
            ConfigureForStatus();
            UpdateSubmitButton();
        }

        // 对照 WPF: private void ConfigureForStatus()
        private void ConfigureForStatus()
        {
            string upstreamRef = _upstreamRemote.Name + "/" + _branchName;
            if (!_status.HasValue)
            {
                // 检测中：按钮由 IsSubmitAllowed 守卫自动禁用
                StatusIcon.Text = "";
                StatusText.Text = FormatTranslate("Checking remote sync: {0}/{1}", _upstreamRemote.Name, _branchName);
                DetailText.Text = Translate("Checking... Please wait.");
                SubmitButtonTitle = Translate("OK");
                ShowCancelButton = false;
                UpdateSubmitButton();
                return;
            }
            switch (_status.Value)
            {
                case ForkSyncStatus.SafeToPush:
                    SetStatusIcon("✓", Colors.Green);
                    StatusText.Text = Translate("Safe to push");
                    DetailText.Text = FormatTranslate(
                        "'{0}' is up-to-date with {1}. You can push without syncing.",
                        _localBranch.Name, upstreamRef);
                    SubmitButtonTitle = Translate("OK");
                    ShowCancelButton = false;
                    break;
                case ForkSyncStatus.ShouldSyncNoConflict:
                    SetStatusIcon("⚠", Colors.Orange);
                    StatusText.Text = Translate("Recommended to sync");
                    DetailText.Text = FormatTranslate(
                        "{0} has new commits that are not in '{1}', but a merge would not produce conflicts. You can push now, but it's recommended to pull first to stay in sync.",
                        upstreamRef, _localBranch.Name);
                    SubmitButtonTitle = Translate("Pull from upstream");
                    CancelButtonTitle = Translate("Skip and push later");
                    ShowCancelButton = true;
                    break;
                case ForkSyncStatus.MustSyncWithConflict:
                    SetStatusIcon("✗", Colors.Red);
                    StatusText.Text = Translate("Conflicts detected");
                    DetailText.Text = FormatTranslate(
                        "{0} has new commits that would conflict with '{1}'. You must pull and resolve the conflicts before pushing.",
                        upstreamRef, _localBranch.Name);
                    SubmitButtonTitle = Translate("Pull and resolve");
                    CancelButtonTitle = Translate("Close");
                    ShowCancelButton = true;
                    break;
                case ForkSyncStatus.NoUpstreamBranch:
                    SetStatusIcon("⚠", Colors.Orange);
                    StatusText.Text = Translate("Upstream branch not found");
                    DetailText.Text = FormatTranslate(
                        "No remote branch '{0}' found on the upstream remote. Please verify the upstream remote and branch name.",
                        upstreamRef);
                    SubmitButtonTitle = Translate("OK");
                    ShowCancelButton = false;
                    break;
                default:
                    SetStatusIcon("⚠", Colors.Orange);
                    StatusText.Text = Translate("Unable to determine sync status");
                    DetailText.Text = FormatTranslate(
                        "Could not determine whether '{0}' would conflict with {1}. Please pull manually to verify.",
                        _localBranch.Name, upstreamRef);
                    SubmitButtonTitle = Translate("OK");
                    ShowCancelButton = false;
                    break;
            }
        }

        // spike 版：用 emoji + 颜色替代 WPF BitmapImage(SuccessIcon/WarningIcon/ErrorIcon)
        private void SetStatusIcon(string emoji, Color color)
        {
            StatusIcon.Text = emoji;
            StatusIcon.Foreground = new SolidColorBrush(color);
        }

        // 对照 WPF: protected override void OnSubmit()
        protected override void OnSubmit()
        {
            // 检测中不应触发任何操作（按钮已禁用，兜底防御）
            if (!_status.HasValue)
            {
                return;
            }
            // 对照 WPF: 对于"安全 push"和"无法判断"等无需操作的状态，点击主按钮即关闭
            if (_status.Value == ForkSyncStatus.SafeToPush
                || _status.Value == ForkSyncStatus.NoUpstreamBranch
                || _status.Value == ForkSyncStatus.Unknown)
            {
                CloseWithOk();
                return;
            }

            // 对照 WPF: 对于需要同步的状态，点击主按钮打开 Pull 窗口让用户拉取 upstream 并解决冲突
            // WPF: RepositoryUserControl.Commands?.ShowPullWindow?.Execute(_repositoryUserControl, null);
            // Avalonia spike: 调用注入的回调
            try
            {
                _onPullRequested?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("ForkSyncCheckWindow pull request callback failed", ex);
            }
            CloseWithOk();
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
