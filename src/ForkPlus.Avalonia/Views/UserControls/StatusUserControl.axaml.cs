using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 StatusUserControl（spike 简化升级版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/StatusUserControl.xaml.cs（278 行）：
    //   - 公共方法：ApplyLocalization / RunAnimation（标题切换动画）
    //   - 私有 Refresh() 由 200ms DispatcherTimer 驱动
    //   - 通过 MainWindow.Instance?.TabManager.ActiveRepositoryUserControl 单例式访问
    //   - 无 Initialize 注入模式
    //
    // 装入路径（WPF）：
    //   ToolbarUserControl.xaml Row 0 → StatusUserControl
    //
    // Avalonia 版差异：
    //   - WPF Visibility.Collapsed/Visible → Avalonia IsVisible=false/true
    //   - WPF Dispatcher.Invoke → Dispatcher.UIThread.Post
    //   - WPF Image.Show()/Hide()/Collapse() → IsVisible=true/false/false
    //   - CenteredDockPanel（自定义控件）→ Grid 替代
    //
    // spike 简化：
    //   - 用 TextBlock 显示状态文本 + emoji（Clean=✓ / Dirty=⚠ / Ahead=⬆ / Behind=⬇）
    //   - SetStatus(string) 解析状态关键词，映射到 emoji 前缀
    //   - RunAnimation 只切换文字，不做 QuadraticEase 缓动
    //   - ActivityManagerPopup / DispatcherTimer 200ms 刷新暂不启用
    public partial class StatusUserControl : UserControl
    {
        public StatusUserControl()
        {
            InitializeComponent();
        }

        // ===== 公共方法（对照 WPF）=====

        // 对照 WPF: public void ApplyLocalization()
        public void ApplyLocalization()
        {
            Console.WriteLine("[Status] ApplyLocalization (spike placeholder)");
        }

        // 对照 WPF: public void RunAnimation(TranslateTransform, from, to, duration, newValue, titleRow, secondaryTitleRow)
        //   标题切换动画（spike 不实现 QuadraticEase 缓动，只切换文字）
        public void RunAnimation(object transform, double from, double to, int duration, string newValue, int titleRow, int secondaryTitleRow)
        {
            if (titleRow == 0 && SecondaryTitleTextBlock != null)
            {
                SecondaryTitleTextBlock.Text = newValue ?? string.Empty;
            }
            else if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = newValue ?? string.Empty;
            }
        }

        // spike 新增：SetStatus(string status)
        //   解析状态关键词，映射到 emoji 前缀并更新 TitleTextBlock
        //   Clean=✓ / Dirty=⚠ / Ahead=⬆ / Behind=⬇ / Unknown=(空)
        public void SetStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                if (StatusEmojiTextBlock != null) StatusEmojiTextBlock.Text = string.Empty;
                if (TitleTextBlock != null) TitleTextBlock.Text = "(no repository)";
                return;
            }

            string lower = status.ToLowerInvariant();
            string emoji = string.Empty;
            if (lower.Contains("clean")) emoji = "✓";
            else if (lower.Contains("dirty") || lower.Contains("conflict")) emoji = "⚠";
            else if (lower.Contains("ahead")) emoji = "⬆";
            else if (lower.Contains("behind")) emoji = "⬇";

            if (StatusEmojiTextBlock != null) StatusEmojiTextBlock.Text = emoji;
            if (TitleTextBlock != null) TitleTextBlock.Text = status;
        }

        // spike 新增：SetProgress(int value, bool isIndeterminate)
        //   更新底部进度条
        public void SetProgress(int value, bool isIndeterminate)
        {
            if (StatusProgressBar != null)
            {
                StatusProgressBar.IsIndeterminate = isIndeterminate;
                if (!isIndeterminate)
                {
                    StatusProgressBar.Value = value;
                }
                StatusProgressBar.IsVisible = true;
            }
        }

        // ===== Button 事件占位（对照 WPF click handler）=====

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Status] CancelButton_Click (spike placeholder)");
        }

        private void ShowActivityManagerToggleButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Status] ShowActivityManagerToggleButton_Click (spike placeholder)");
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Status] FilterButton_Click (spike placeholder)");
        }
    }
}
