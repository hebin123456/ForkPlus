using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.Accounts;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 NotificationManagerUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/NotificationManagerUserControl.xaml.cs（164 行）：
    //   - Popup + ToggleButton parentButton 控制 IsVisible（OnVisualParentChanged 解析 Popup + PlacementTarget）
    //   - NotificationManager.Current.IsUpdatingChanged → RefreshBusyIndicator
    //   - NotificationManager.Current.NotificationsChanged → RefreshNotifications
    //   - RefreshButton.Click → NotificationManager.Current.Refresh()
    //   - ListBox_SelectionChanged：UnsetUnread + OpenInBrowser + HidePopup
    //   - RefreshNotifications：NotificationManager.Current.Notifications.Map(VM) → _notifications
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF Popup + ToggleButton parentButton → spike 直接用 IsVisible 控制（spike 不嵌套 Popup）
    //   - WPF NotificationManager.Current 单例依赖 → 注入回调
    //     （onRefresh 触发刷新 + notificationsChanged 事件 + isUpdatingChanged 事件）
    //   - WPF e.Uri.OpenInBrowser() → onOpenUrl 回调注入
    //   - WPF _parentButton.IsChecked = true/false → spike Show()/Hide() 方法控制 IsVisible
    //   - WPF OnVisualParentChanged 解析 Popup → spike 跳过
    //   - WPF NotificationViewModel : INotifyPropertyChanged → spike POCO
    public partial class NotificationManagerUserControl : UserControl
    {
        // ===== NotificationViewModel POCO（对照 WPF NotificationViewModel.cs：INotifyPropertyChanged）=====
        // spike 简化为 POCO（重设 ItemsSource 触发刷新）
        public class NotificationViewModel
        {
            public GitServiceNotification Notification { get; }
            public string Title => Notification.RepositoryFullName;
            public string TargetId { get; }
            public string RepositoryAvatarUrl => Notification.RepositoryAvatarUrl;
            public string Description => Notification.Title;
            public DateTime DateTime => Notification.Date;
            public string TargetTypeIcon => GetTargetTypeIcon(Notification.TargetType);
            public string TargetUrl => Notification.TargetUrl;
            public bool Unread => Notification.Unread;

            public NotificationViewModel(GitServiceNotification notification)
            {
                Notification = notification;
                TargetId = UserFriendlyId(notification);
            }

            // 对照 WPF: private static string UserFriendlyId(GitServiceNotification notification)
            //   Commit → TargetId.Substring(0, 7)
            //   Issue/PullRequest → "#" + TargetId
            private static string UserFriendlyId(GitServiceNotification notification)
            {
                string id = notification.TargetId ?? string.Empty;
                return notification.TargetType switch
                {
                    GitServiceNotificationTargetType.Commit => id.Length > 7 ? id.Substring(0, 7) : id,
                    GitServiceNotificationTargetType.Issue => "#" + id,
                    GitServiceNotificationTargetType.PullRequest => "#" + id,
                    _ => "#" + id,
                };
            }

            // 对照 WPF: Notification.TargetType.Icon() → spike emoji 映射
            // Commit → ✓ / Issue → 📝 / PullRequest → 🔀
            private static string GetTargetTypeIcon(GitServiceNotificationTargetType targetType)
            {
                return targetType switch
                {
                    GitServiceNotificationTargetType.Commit => "✓",
                    GitServiceNotificationTargetType.Issue => "📝",
                    GitServiceNotificationTargetType.PullRequest => "🔀",
                    _ => "ℹ",
                };
            }
        }

        // ===== 私有字段（对照 WPF）=====
        private readonly ObservableCollection<NotificationViewModel> _notifications = new ObservableCollection<NotificationViewModel>();
        private Action<string> _onOpenUrl;

        // ===== 构造函数（对照 WPF）=====
        public NotificationManagerUserControl()
        {
            InitializeComponent();
            HeaderLabel.Text = Translate("Notifications");
            ListBox.ItemsSource = _notifications;
        }

        // ===== Initialize（spike 新增，注入回调）=====
        // 对照 WPF:
        //   NotificationManager.Current.IsUpdatingChanged += NotificationManager_IsUpdatingChanged;
        //   NotificationManager.Current.NotificationsChanged += NotificationManager_NotificationsChanged;
        //   RefreshButton.Click += () => NotificationManager.Current.Refresh();
        // spike 版:
        //   - 调用方通过 Subscribe(isUpdatingChanged, notificationsChanged, onRefresh) 注入事件回调
        //   - _onOpenUrl 回调替代 e.Uri.OpenInBrowser()
        public void Initialize(Action<string> onOpenUrl = null)
        {
            _onOpenUrl = onOpenUrl;
        }

        // ===== ApplyLocalization（对照 WPF: public void ApplyLocalization()）=====
        // 对照 WPF: HeaderLabel.Text = PreferencesLocalization.Translate("Notifications", ForkPlusSettings.Default.UiLanguage)
        public void ApplyLocalization()
        {
            HeaderLabel.Text = Translate("Notifications");
        }

        // ===== ShowPopup / HidePopup（对照 WPF）=====
        // 对照 WPF: public void ShowPopup() { _parentButton.IsChecked = true; }
        //           public void HidePopup() { _parentButton.IsChecked = false; }
        // spike 版: 直接设置 IsVisible（无 ToggleButton parent）
        public void ShowPopup()
        {
            IsVisible = true;
        }

        public void HidePopup()
        {
            IsVisible = false;
        }

        // ===== RefreshButton_Click（对照 WPF RefreshButton.Click）=====
        // 对照 WPF: NotificationManager.Current.Refresh()
        // spike 版: 调用注入的 OnRefresh 回调（由调用方处理 NotificationManager 单例）
        public event EventHandler RefreshRequested;

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        // ===== ListBox_SelectionChanged（对照 WPF）=====
        // 对照 WPF: private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //   WPF: if (ListBox.SelectedItem is NotificationViewModel vm)
        //          NotificationManager.Current.UnsetUnread(vm.Notification);
        //          new Uri(vm.TargetUrl).OpenInBrowser();
        //          HidePopup();
        //          _notifications.Clear();
        // spike 版: 同样逻辑（UnsetUnread 留待调用方处理 + onOpenUrl 回调）
        public event EventHandler<NotificationViewModel> NotificationSelected;

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 0 && ListBox.SelectedItem is NotificationViewModel vm)
            {
                NotificationSelected?.Invoke(this, vm);
                _onOpenUrl?.Invoke(vm.TargetUrl);
                HidePopup();
                _notifications.Clear();
            }
        }

        // ===== RefreshBusyIndicator（对照 WPF: private void RefreshBusyIndicator()）=====
        // 对照 WPF:
        //   if (NotificationManager.Current.IsUpdating) { BusyIndicator.Show(); RefreshButton.Hide(); }
        //   else { BusyIndicator.Hide(); RefreshButton.Show(); }
        // spike 版: 直接设置 IsVisible
        public void RefreshBusyIndicator(bool isUpdating)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (BusyIndicator != null) BusyIndicator.IsVisible = isUpdating;
                if (RefreshButton != null) RefreshButton.IsVisible = !isUpdating;
            });
        }

        // ===== RefreshNotifications（对照 WPF: private void RefreshNotifications()）=====
        // 对照 WPF:
        //   NotificationViewModel[] array = NotificationManager.Current.Notifications.Map(x => new NotificationViewModel(x));
        //   _notifications.Clear();
        //   foreach (var item in array) _notifications.Add(item);
        // spike 版: 同样逻辑
        public void RefreshNotifications(GitServiceNotification[] notifications)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _notifications.Clear();
                if (notifications != null)
                {
                    foreach (GitServiceNotification n in notifications)
                    {
                        _notifications.Add(new NotificationViewModel(n));
                    }
                }
            });
        }

        // ===== 辅助方法（对照 WPF）=====

        // 对照 WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        // spike: ServiceLocator.Localization.Translate
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
