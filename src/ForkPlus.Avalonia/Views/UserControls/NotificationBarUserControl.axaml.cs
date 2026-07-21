using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ForkPlus.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.4：Avalonia 版 NotificationBarUserControl（完整迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/NotificationBarUserControl.xaml.cs（395 行）：
    //   - 实现 INotifyPropertyChanged（IsControlVisible 属性驱动 Border 可见性）
    //   - 构造函数：Button1/Button2/Button3/AbortButton Click 订阅 + AbortButton.Content = Translate("Abort")
    //   - Initialize(RepositoryUserControl)：注入父控件
    //   - Refresh()：读 _repositoryUserControl.RepositoryStatus?.RepositoryState，按状态类型
    //     （MergeInProgress/RebaseInProgress/CherryPickInProgress/SequencerInProgress/
    //      RevertInProgress/SquashInProgress/AmInProgress/UnmergedIndex/BisectInProgress/OK）
    //     构造 Inline 列表 + ShowNotificationBar / ShowRebaseNotificationBar + UpdateButton
    //   - Button1_Click/Button2_Click/Button3_Click/AbortButton_Click：按 RepositoryState 分发
    //     （ActivateCommitView / Bisect Good/Bad/Skip / ShowAbortConflictWindowCommand /
    //      ShowGitignoreTemplateWindow / DismissGitignoreSuggestion）
    //   - UpdateGitignoreSuggestion()：检查 .gitignore 是否存在 + FilesCount，显示建议
    //   - ShowNotificationBar(string) / ShowNotificationBar(IEnumerable<Inline>) /
    //     ShowRebaseNotificationBar(inlines, done, total, additionalInlines)
    //   - HideNotificationBar()
    //   - UpdateButton(Button, title, show)：设置按钮 Content + Visibility
    //   - Translate(text)：PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
    //
    // Avalonia 版差异（spike 简化）：
    //   - MainWindow.Instance 依赖 → 注入 Action? onRepositoryRefresh 回调（构造函数 + Initialize）
    //   - WPF Inline 列表（Run + CommandHyperlink）→ 纯字符串 Message（NotificationViewModel）
    //   - 通知图标 PNG（Warning.png）→ emoji TextBlock（ℹ/⚠/✗/✓，按 NotificationType 切换）
    //   - 动画 CollapseAnimation → 简化 IsVisible 切换
    //   - PreferencesLocalization.Translate → ServiceLocator.Localization.Translate
    //   - Image.Show()/Hide()/Collapse() 扩展方法 → IsVisible = true/false/false
    //   - Button.Collapse()/Show() 扩展方法 → IsVisible = false/true
    //   - INotifyPropertyChanged 保留（IsControlVisible 属性），但 Border.IsVisible 直接在代码中设置
    //   - WPF RepositoryState 分支逻辑（依赖 RepositoryUserControl.RepositoryStatus）→ 简化为
    //     NotificationViewModel 驱动：调用方（RepositoryUserControl）构造 vm 后调 ShowNotification(vm)
    //
    // 本 spike 版实现：
    //   - NotificationViewModel POCO（Title/Message/Type/ActionLabel/Action/SecondaryActionLabel/
    //     SecondaryAction/ThirdActionLabel/ThirdAction/AbortVisible/RebaseProgress）
    //   - NotificationType 枚举（Info/Warning/Error/Success）→ emoji 映射
    //   - Refresh()：触发 onRepositoryRefresh 回调 + 重渲染当前通知
    //   - ShowNotification(NotificationViewModel)：真实显示（图标 + 文本 + 按钮 + 进度条）
    //   - Clear()：隐藏通知栏
    //   - Button1/2/3_Click：调用 vm.Action/SecondaryAction/ThirdAction 回调
    //   - AbortButton_Click：调用 vm.AbortAction 回调（spike 默认 null → 仅 Clear）
    //
    // 装入路径（WPF）：RepositoryUserControl.xaml Row 0 → NotificationBarUserControl
    public partial class NotificationBarUserControl : UserControl, INotifyPropertyChanged
    {
        // ===== NotificationType 枚举（对照 WPF 隐式状态映射，spike 显式化）=====
        public enum NotificationType
        {
            Info,      // ℹ — 对照 WPF OK / SequencerInProgress
            Warning,   // ⚠ — 对照 WPF MergeInProgress / RebaseInProgress / CherryPickInProgress /
                       //       RevertInProgress / SquashInProgress / AmInProgress / UnmergedIndex /
                       //       BisectInProgress / GitignoreSuggestion
            Error,     // ✗ — 对照 WPF 冲突未解决状态
            Success    // ✓ — 对照 WPF "All conflicts fixed"
        }

        // ===== NotificationViewModel POCO（spike 新增，替代 WPF Inline 列表 + Button 参数散布）=====
        // 对照 WPF Refresh() 中分散的 ShowNotificationBar(list) + UpdateButton(Button1, "Resolve", show) 调用，
        // spike 版统一收敛为一个 POCO，调用方构造后传给 ShowNotification(vm)。
        public class NotificationViewModel
        {
            // 通知标题（对照 WPF 无独立标题，spike 新增，显示在 Message 前）
            public string Title { get; set; }

            // 通知正文（对照 WPF ShowNotificationBar 的 Inline 列表拼接文本）
            public string Message { get; set; }

            // 通知类型（决定 emoji 图标：ℹ/⚠/✗/✓）
            public NotificationType Type { get; set; } = NotificationType.Warning;

            // Button1 标签 + 回调（对照 WPF Button1 + UpdateButton(Button1, title, show)）
            public string ActionLabel { get; set; }
            public Action Action { get; set; }

            // Button2 标签 + 回调（对照 WPF Button2，Bisect Bad / DismissGitignoreSuggestion）
            public string SecondaryActionLabel { get; set; }
            public Action SecondaryAction { get; set; }

            // Button3 标签 + 回调（对照 WPF Button3，Bisect Skip）
            public string ThirdActionLabel { get; set; }
            public Action ThirdAction { get; set; }

            // AbortButton 可见性（对照 WPF AbortButton.Show()/Collapse()）
            public bool AbortVisible { get; set; } = true;

            // AbortButton 回调（对照 WPF AbortButton_Click → ShowAbortConflictWindowCommand）
            public Action AbortAction { get; set; }

            // Rebase 进度（对照 WPF ShowRebaseNotificationBar(done, total)）
            // Done < 0 或 Total <= 0 表示非 rebase 模式（隐藏 RebaseContainer）
            public int RebaseDone { get; set; } = -1;
            public int RebaseTotal { get; set; } = 0;
            public string RebaseAdditionalMessage { get; set; }
        }

        // ===== 私有字段（对照 WPF）=====
        private readonly IServiceProvider _serviceProvider;
        private Action _onRepositoryRefresh;
        private object _repositoryUserControl; // 对照 WPF RepositoryUserControl（spike 用 object 占位）
        private bool _showingGitignoreSuggestion; // 对照 WPF _showingGitignoreSuggestion
        private NotificationViewModel _currentNotification;

        private bool _isControlVisible;
        // 对照 WPF: public bool IsControlVisible（INotifyPropertyChanged 驱动 Border 可见性）
        public bool IsControlVisible
        {
            get => _isControlVisible;
            set
            {
                if (value != _isControlVisible)
                {
                    _isControlVisible = value;
                    if (NotificationBorder != null)
                    {
                        NotificationBorder.IsVisible = value;
                    }
                    NotifyPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // ===== 构造函数（对照 WPF 无参构造，spike 注入 IServiceProvider + 可选回调）=====
        public NotificationBarUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            Button1.Click += Button1_Click;
            Button2.Click += Button2_Click;
            Button3.Click += Button3_Click;
            AbortButton.Click += AbortButton_Click;
            AbortButton.Content = Translate("Abort");
        }

        // ===== Initialize（对照 WPF: public void Initialize(RepositoryUserControl repositoryUserControl)）=====
        // spike 版额外接受 onRepositoryRefresh 回调（替代 WPF MainWindow.Instance 依赖）
        public void Initialize(object repositoryUserControl, Action onRepositoryRefresh = null)
        {
            _repositoryUserControl = repositoryUserControl;
            _onRepositoryRefresh = onRepositoryRefresh;
        }

        // ===== Refresh()（对照 WPF: public void Refresh()）=====
        // WPF 版读 _repositoryUserControl.RepositoryStatus?.RepositoryState 按 10 种状态分支。
        // spike 版：Avalonia RepositoryUserControl 是 stub 无 RepositoryStatus 属性，
        // 改为触发 onRepositoryRefresh 回调让父控件刷新后调 ShowNotification(vm)。
        // 若有当前通知则重渲染。
        public void Refresh()
        {
            _showingGitignoreSuggestion = false;
            // 隐藏所有操作按钮，只保留 AbortButton（对照 WPF Refresh() 开头）
            if (Button1 != null) Button1.IsVisible = false;
            if (Button2 != null) Button2.IsVisible = false;
            if (Button3 != null) Button3.IsVisible = false;
            if (AbortButton != null)
            {
                AbortButton.IsVisible = true;
                AbortButton.Content = Translate("Abort");
            }

            // 触发父控件刷新回调（对照 WPF Refresh 间接依赖 RepositoryUserControl.Refresh 链）
            _onRepositoryRefresh?.Invoke();

            // 重渲染当前通知（如果有）
            if (_currentNotification != null)
            {
                ShowNotification(_currentNotification);
            }
            else
            {
                HideNotificationBar();
            }
        }

        // ===== ShowNotification(NotificationViewModel)（spike 新增，替代 WPF ShowNotificationBar）=====
        // 对照 WPF: private void ShowNotificationBar(string message) / ShowNotificationBar(IEnumerable<Inline>)
        //           / ShowRebaseNotificationBar(inlines, done, total, additionalInlines)
        // spike 版统一入口：根据 vm.RebaseDone 判断走普通通知还是 rebase 进度通知。
        public void ShowNotification(NotificationViewModel vm)
        {
            if (vm == null)
            {
                HideNotificationBar();
                return;
            }

            _currentNotification = vm;

            // 设置状态图标 emoji（对照 WPF StatusImage PNG）
            if (StatusIconTextBlock != null)
            {
                StatusIconTextBlock.Text = GetEmojiForType(vm.Type);
            }

            // 判断是否为 rebase 进度通知（对照 WPF ShowRebaseNotificationBar）
            bool isRebase = vm.RebaseDone >= 0 && vm.RebaseTotal > 0;

            if (isRebase)
            {
                // 对照 WPF: RebaseContainer.Show() / NotificationTextBlock.Collapse()
                if (NotificationTextBlock != null) NotificationTextBlock.IsVisible = false;
                if (RebaseContainer != null) RebaseContainer.IsVisible = true;

                // 对照 WPF: RebaseNotificationTextBlock.Inlines.AddRange(inlines)
                if (RebaseNotificationTextBlock != null)
                {
                    RebaseNotificationTextBlock.Text = vm.Message ?? string.Empty;
                }

                // 对照 WPF: RebaseProgressBar.Value = done; RebaseProgressBar.Maximum = total
                if (RebaseProgressBar != null)
                {
                    RebaseProgressBar.Value = vm.RebaseDone;
                    RebaseProgressBar.Maximum = vm.RebaseTotal;
                }

                // 对照 WPF: RebaseAdditionalNotificationTextBlock.Inlines.AddRange(additionalInlines)
                if (RebaseAdditionalNotificationTextBlock != null)
                {
                    RebaseAdditionalNotificationTextBlock.Text = vm.RebaseAdditionalMessage ?? string.Empty;
                }
            }
            else
            {
                // 对照 WPF: NotificationTextBlock.Show() / RebaseContainer.Collapse()
                if (NotificationTextBlock != null) NotificationTextBlock.IsVisible = true;
                if (RebaseContainer != null) RebaseContainer.IsVisible = false;

                // 对照 WPF: NotificationTextBlock.Inlines.Clear() + AddRange(inlines)
                var fullText = string.IsNullOrEmpty(vm.Title)
                    ? (vm.Message ?? string.Empty)
                    : $"{vm.Title}: {vm.Message}";
                if (NotificationTextBlock != null)
                {
                    NotificationTextBlock.Text = fullText;
                }
            }

            // 设置按钮（对照 WPF UpdateButton）
            UpdateButton(Button1, vm.ActionLabel, vm.Action != null || vm.ActionLabel != null);
            UpdateButton(Button2, vm.SecondaryActionLabel, vm.SecondaryAction != null || vm.SecondaryActionLabel != null);
            UpdateButton(Button3, vm.ThirdActionLabel, vm.ThirdAction != null || vm.ThirdActionLabel != null);

            // 设置 AbortButton 可见性（对照 WPF AbortButton.Show()/Collapse()）
            if (AbortButton != null)
            {
                AbortButton.IsVisible = vm.AbortVisible;
                AbortButton.Content = Translate("Abort");
            }

            IsControlVisible = true;
        }

        // ===== Clear()（对照 WPF HideNotificationBar，spike 重命名为 Clear 让调用方更直觉）=====
        // 对照 WPF: private void HideNotificationBar() { IsControlVisible = false; }
        public void Clear()
        {
            _currentNotification = null;
            _showingGitignoreSuggestion = false;
            HideNotificationBar();
        }

        // ===== Button 事件（对照 WPF Button1_Click/Button2_Click/Button3_Click/AbortButton_Click）=====

        // 对照 WPF: private void Button1_Click(object sender, RoutedEventArgs e)
        //   WPF: 按 _showingGitignoreSuggestion / RepositoryState 分发
        //   spike: 调用 vm.Action 回调
        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            if (_showingGitignoreSuggestion)
            {
                // 对照 WPF: ShowGitignoreTemplateWindow()
                _currentNotification?.Action?.Invoke();
                return;
            }
            _currentNotification?.Action?.Invoke();
        }

        // 对照 WPF: private void Button2_Click(object sender, RoutedEventArgs e)
        //   WPF: DismissGitignoreSuggestion / Bisect Bad
        //   spike: 调用 vm.SecondaryAction 回调
        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (_showingGitignoreSuggestion)
            {
                // 对照 WPF: DismissGitignoreSuggestion()
                _currentNotification?.SecondaryAction?.Invoke();
                return;
            }
            _currentNotification?.SecondaryAction?.Invoke();
        }

        // 对照 WPF: private void Button3_Click(object sender, RoutedEventArgs e)
        //   WPF: Bisect Skip
        //   spike: 调用 vm.ThirdAction 回调
        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            _currentNotification?.ThirdAction?.Invoke();
        }

        // 对照 WPF: private void AbortButton_Click(object sender, RoutedEventArgs e)
        //   WPF: new ShowAbortConflictWindowCommand().Execute(_repositoryUserControl)
        //   spike: 调用 vm.AbortAction 回调（默认 null → 仅 Clear）
        private void AbortButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNotification?.AbortAction != null)
            {
                _currentNotification.AbortAction.Invoke();
            }
            else
            {
                Clear();
            }
        }

        // ===== Gitignore 建议辅助方法（对照 WPF UpdateGitignoreSuggestion / DismissGitignoreSuggestion）=====
        // 对照 WPF: private void UpdateGitignoreSuggestion()
        //   WPF: 检查 GitModule + .gitignore 文件存在 + FilesCount + GitignoreSuggestionDismissed
        //   spike: 公开为 ShowGitignoreSuggestion()，由调用方决定何时显示（因为 GitModule 不在 Avalonia stub 中）
        public void ShowGitignoreSuggestion()
        {
            _showingGitignoreSuggestion = true;
            var vm = new NotificationViewModel
            {
                Title = string.Empty,
                Message = Translate("Consider adding a .gitignore file to your repository"),
                Type = NotificationType.Info,
                ActionLabel = Translate("Add .gitignore…"),
                Action = null, // 对照 WPF: ShowGitignoreTemplateWindow() — 调用方注入
                SecondaryActionLabel = Translate("Close"),
                SecondaryAction = DismissGitignoreSuggestion,
                AbortVisible = false
            };
            ShowNotification(vm);
        }

        // 对照 WPF: private void DismissGitignoreSuggestion()
        //   WPF: gitModule.Settings.GitignoreSuggestionDismissed = true; gitModule.Settings.Save()
        //   spike: 仅隐藏通知栏（Settings 持久化由调用方处理）
        public void DismissGitignoreSuggestion()
        {
            _showingGitignoreSuggestion = false;
            HideNotificationBar();
        }

        // ===== 私有辅助方法（对照 WPF）=====

        // 对照 WPF: private void UpdateButton(Button button, string title, bool show = true)
        private void UpdateButton(Button button, string title, bool show)
        {
            if (button == null) return;
            button.IsVisible = show;
            if (show && title != null)
            {
                button.Content = Translate(title);
            }
        }

        // 对照 WPF: private void HideNotificationBar()
        private void HideNotificationBar()
        {
            IsControlVisible = false;
        }

        // spike 新增：NotificationType → emoji 映射
        private static string GetEmojiForType(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Info:    return "ℹ";
                case NotificationType.Warning: return "⚠";
                case NotificationType.Error:   return "✗";
                case NotificationType.Success: return "✓";
                default:                       return "⚠";
            }
        }

        // 对照 WPF: private static string Translate(string text)
        //   WPF: PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage)
        //   spike: ServiceLocator.Localization.Translate(text, lang)
        private static string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // ServiceLocator.Localization 可能在设计时为 null
            if (ServiceLocator.Localization == null) return text;
            try
            {
                return ServiceLocator.Localization.Translate(text, ForkPlus.Settings.ForkPlusSettings.Default.UiLanguage);
            }
            catch
            {
                return text;
            }
        }

        // 对照 WPF: private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
