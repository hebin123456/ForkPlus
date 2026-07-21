using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 AccountTabItem（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/AccountTabItem.xaml.cs（109 行）：
    //   - JobQueue 异步刷新状态（account.Service.GetUser() → StatusEllipse 绿/红 + StatusTextBlock 文本）
    //   - Refresh(Account account)：填充 ServerType/AuthenticationType/Username/Status/NotificationsCheckBox
    //   - NotificationsCheckBox_Checked：更新 account.EnableNotifications + Save + NotificationManager.Current.Refresh
    //   - UpdateTokenButton_Click：触发 UpdateTokenButtonClicked 事件
    //   - Translate(text)：PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF TabItem 基类 → Avalonia UserControl（spike 不嵌套 TabControl）
    //   - WPF Image PNG ServiceTypeImage → TextBlock emoji
    //   - WPF Dispatcher.Async → Dispatcher.UIThread.Post
    //   - WPF Visibility.Hidden/Visible → IsVisible=false/true
    //   - WPF CheckBox.Checked/Unchecked → IsCheckedChanged 事件
    //   - WPF PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //
    // spike 简化：
    //   - account.Service.GetUser() 真实调用留待后续 Phase（需 Git 服务网络层），
    //     spike 版用 SetStatus(string, bool) 公共方法允许外部直接设置状态
    //   - JobQueue → spike 不使用
    //   - INotificationGitService 检查 → 由 Refresh 时 supportsNotifications 参数传入
    //   - NotificationManager.Current 依赖 → 由 onNotificationsToggled 回调注入
    public partial class AccountTabItem : UserControl
    {
        // ===== EventArgs<T> 通用泛型（对照 WPF EventArgs<Account>）=====
        public class EventArgs<T> : EventArgs
        {
            public T Value { get; }

            public EventArgs(T value)
            {
                Value = value;
            }
        }

        // ===== 私有字段（对照 WPF）=====
        private Account _account;
        private bool _refreshInProgress;

        // ===== 事件（对照 WPF: public event EventHandler<EventArgs<Account>> UpdateTokenButtonClicked）=====
        public event EventHandler<EventArgs<Account>> UpdateTokenButtonClicked;

        // ===== 构造函数（对照 WPF）=====
        public AccountTabItem()
        {
            InitializeComponent();
        }

        // ===== Refresh（对照 WPF: public void Refresh(Account account)）=====
        // 对照 WPF:
        //   ServerTypeImage.Source = account.ServiceType.Icon();
        //   ServerTypeTextBlock.Text = account.ServiceType.FriendlyName();
        //   UserNameTextBlock.Text = account.Username;
        //   AuthenticationTypeTextBlock.Text = account.AuthenticationType.FriendlyName();
        //   if (account.Service is INotificationGitService) { NotificationsCheckBox.Show(); ... }
        //   else { NotificationsCheckBox.Hide(); }
        //   StatusTextBlock.Text = Translate("Updating...");
        //   StatusBusyIndicator.Show(); StatusEllipse.Hide();
        //   _jobQueue.Add(... GetUser() ...)
        // spike 版：
        //   - ServiceType.Image → GetServiceTypeEmoji(RemoteType) emoji 映射
        //   - ServiceType.FriendlyName → RemoteType.ToString()
        //   - AuthenticationType.FriendlyName → AuthenticationType.ToString()
        //   - INotificationGitService 检查 → 由 supportsNotifications 参数传入（spike 不依赖 Service）
        //   - GetUser() 异步调用 → spike 跳过，直接显示 "Ready"
        public void Refresh(Account account, bool supportsNotifications = false)
        {
            _account = account;
            if (account == null) return;

            ServerTypeImage.Text = GetServiceTypeEmoji(account.ServiceType);
            ServerTypeTextBlock.Text = account.ServiceType.ToString();
            UserNameTextBlock.Text = account.Username;
            AuthenticationTypeTextBlock.Text = account.AuthenticationType.ToString();

            if (supportsNotifications)
            {
                NotificationsCheckBox.IsVisible = true;
                _refreshInProgress = true;
                NotificationsCheckBox.IsChecked = account.EnableNotifications;
                _refreshInProgress = false;
            }
            else
            {
                NotificationsCheckBox.IsVisible = false;
            }

            StatusTextBlock.Text = Translate("Updating...");
            StatusBusyIndicator.IsVisible = true;
            StatusEllipse.IsVisible = false;

            // spike 简化：不调用 account.Service.GetUser()（需要 Git 服务网络层）
            // 直接显示 Ready 状态，调用方可通过 SetStatus("Online", true) 显式设置
            Dispatcher.UIThread.Post(() =>
            {
                StatusBusyIndicator.IsVisible = false;
                StatusEllipse.IsVisible = true;
                StatusEllipse.Fill = Brushes.Green;
                StatusTextBlock.Text = Translate("Ready");
            });
        }

        // ===== SetStatus（spike 新增，替代 WPF GetUser 异步调用的状态更新）=====
        // 调用方可在 Refresh 后调用此方法直接设置最终状态（成功/失败）
        public void SetStatus(string statusText, bool isSuccess)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusBusyIndicator.IsVisible = false;
                StatusEllipse.IsVisible = true;
                StatusEllipse.Fill = isSuccess ? Brushes.Green : Brushes.Red;
                StatusTextBlock.Text = statusText;
            });
        }

        // ===== NotificationsCheckBox_Checked（对照 WPF）=====
        // 对照 WPF: private void NotificationsCheckBox_Checked(object sender, RoutedEventArgs e)
        //   WPF: if (!_refreshInProgress) {
        //          _account.EnableNotifications = NotificationsCheckBox.IsChecked.GetValueOrDefault(true);
        //          AccountManager.Current.Save();
        //          NotificationManager.Current.Refresh(); }
        // spike 版: 调用方注入 onNotificationsToggled 回调（替代 AccountManager/NotificationManager 单例依赖）
        private void NotificationsCheckBox_IsCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!_refreshInProgress && _account != null)
            {
                _account.EnableNotifications = NotificationsCheckBox.IsChecked.GetValueOrDefault(true);
                // spike 不直接调用 AccountManager/NotificationManager 单例
                // 真实 Save + Refresh 由调用方处理（Avalonia 工程暂未迁移单例）
            }
        }

        // ===== UpdateTokenButton_Click（对照 WPF）=====
        // 对照 WPF: private void UpdateTokenButton_Click(object sender, RoutedEventArgs e)
        //   WPF: if (_account != null) UpdateTokenButtonClicked?.Invoke(this, new EventArgs<Account>(_account));
        private void UpdateTokenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_account != null)
            {
                UpdateTokenButtonClicked?.Invoke(this, new EventArgs<Account>(_account));
            }
        }

        // ===== 辅助方法（对照 WPF）=====

        // 对照 WPF: ServiceType.Icon() → spike emoji 映射
        private static string GetServiceTypeEmoji(RemoteType serviceType)
        {
            switch (serviceType)
            {
                case RemoteType.Github:
                case RemoteType.GithubEnterprise:
                    return "🐙"; // GitHub Octocat
                case RemoteType.Gitlab:
                case RemoteType.GitlabServer:
                    return "🦊"; // GitLab Fox
                case RemoteType.Bitbucket:
                case RemoteType.BitbucketServer:
                    return "🪣"; // Bitbucket Bucket
                case RemoteType.Gitea:
                    return "🍵"; // Gitea Tea
                case RemoteType.Azure:
                case RemoteType.Visualstudio:
                    return "☁"; // Azure cloud
                default:
                    return "🐙";
            }
        }

        // 对照 WPF: private static string Translate(string text)
        //   WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        //   spike: ServiceLocator.Localization.Translate(text, lang)（设计时 null 容错）
        private static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (ServiceLocator.Localization == null) return text;
            try
            {
                return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
            }
            catch
            {
                return text;
            }
        }
    }
}
