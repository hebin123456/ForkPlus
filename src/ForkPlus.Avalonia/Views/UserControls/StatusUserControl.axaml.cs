using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Phase 3.10：Avalonia 版 StatusUserControl 骨架（spike 简化版）。
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
    // 本 spike 版策略：
    //   - CenteredDockPanel 用 Grid 替代
    //   - ActivityManagerUserControl 用空 Popup 占位（未在 axaml 创建，spike 不实现）
    //   - 标题动画用 Avalonia TranslateTransform（API 一致，但 spike 不实现动画切换）
    //   - BusyIndicator 用 ProgressBar 占位
    //   - DispatcherTimer 用 Avalonia DispatcherTimer（API 一致，spike 不启用）
    //   - 公共方法签名保留，body stub
    //
    // 本 spike 版暂不迁移：
    //   - RunAnimation 标题切换动画的精确缓动（QuadraticEase）
    //   - ActivityManagerPopup 真实作业列表
    //   - 分支过滤逻辑（UpdateReferenceFilter.ToggleActiveBranchFilter）
    //   - DispatcherTimer 200ms 刷新
    //
    // 本 spike 版验证：
    //   - Grid 3 列 × 3 行布局正确显示
    //   - 标题占位文字可见
    //   - 底部进度条占位可见
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
        //   标题切换动画（spike 不实现 QuadraticEase 缓动）
        public void RunAnimation(object transform, double from, double to, int duration, string newValue, int titleRow, int secondaryTitleRow)
        {
            Console.WriteLine($"[Status] RunAnimation (spike placeholder): from={from}, to={to}, newValue={newValue}");
            // spike 版只切换文字，不做动画
            if (titleRow == 0 && SecondaryTitleTextBlock != null)
            {
                SecondaryTitleTextBlock.Text = newValue ?? string.Empty;
            }
            else if (TitleTextBlock != null)
            {
                TitleTextBlock.Text = newValue ?? string.Empty;
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
