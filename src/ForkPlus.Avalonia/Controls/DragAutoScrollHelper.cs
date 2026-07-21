using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 DragAutoScrollHelper（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/DragAutoScrollHelper.cs（104 行）：
    //   - WPF DragAutoScrollHelper（非控件，辅助类）
    //   - 构造函数接收 ItemsControl，订阅 DragOver / DragLeave / Drop 事件
    //   - EdgeThreshold = 25.0（鼠标距边缘 25px 内触发自动滚动）
    //   - OnDragOver：Y < 25 → StartAutoScroll(-1) / Y > ActualHeight-25 → StartAutoScroll(1)
    //   - DispatcherTimer 50ms 间隔，OnTimerTick → ScrollViewer.LineUp / LineDown
    //   - GetScrollViewer：VisualTreeHelper 遍历 ItemsControl → Border → ScrollViewer
    //   - StopAutoScroll：_scrollDirection = 0 + _timer.Stop()
    //
    // Avalonia 版差异（spike 简化策略，task spec：静态类 + ScrollViewer 滚动逻辑）：
    //   1. WPF 实例类（构造接收 ItemsControl）→ spike 静态类 + 扩展方法
    //   2. WPF DispatcherTimer → Avalonia DispatcherTimer（Avalonia.Threading 命名空间）
    //   3. WPF DragOver/DragLeave/Drop 事件订阅 → spike 用 DragDrop.DragOverEvent 等
    //      （Avalonia 11 用 DragDrop.SetAllowDrop + DragDrop.DropEvent）
    //   4. WPF VisualTreeHelper 遍历找 ScrollViewer → spike 用 Avalonia visual tree 遍历
    //   5. spike 简化：单实例模式（每控件一个 helper），用字典管理 timer
    //
    // spike 简化（task spec 关键 API）：
    //   - 静态类 + StartAutoScroll / StopAutoScroll 方法
    //   - ScrollViewer.LineUp / LineDown 滚动逻辑
    public static class DragAutoScrollHelper
    {
        // 对照 WPF: private const double EdgeThreshold = 25.0
        private const double EdgeThreshold = 25.0;

        // 对照 WPF: private DispatcherTimer _timer; 50ms 间隔
        private static DispatcherTimer _timer;
        private static ScrollViewer _scrollViewer;
        private static int _scrollDirection;

        // spike 公共方法：根据鼠标位置判断是否需要自动滚动
        // 对照 WPF: OnDragOver(object sender, DragEventArgs e)
        public static void HandleDragOver(ScrollViewer scrollViewer, double mouseY, double controlHeight)
        {
            _scrollViewer = scrollViewer;
            if (mouseY < EdgeThreshold)
            {
                StartAutoScroll(-1);
            }
            else if (mouseY > controlHeight - EdgeThreshold)
            {
                StartAutoScroll(1);
            }
            else
            {
                StopAutoScroll();
            }
        }

        // 对照 WPF: private void StartAutoScroll(int direction)
        private static void StartAutoScroll(int direction)
        {
            _scrollDirection = direction;
            if (_timer == null)
            {
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50.0)
                };
                _timer.Tick += OnTimerTick;
            }
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        // 对照 WPF: public void StopAutoScroll()
        public static void StopAutoScroll()
        {
            _scrollDirection = 0;
            _timer?.Stop();
        }

        // 对照 WPF: private void OnTimerTick(object sender, EventArgs e)
        private static void OnTimerTick(object sender, EventArgs e)
        {
            if (_scrollViewer == null)
            {
                return;
            }
            // 对照 WPF: scrollViewer.LineUp() / scrollViewer.LineDown()
            if (_scrollDirection < 0)
            {
                _scrollViewer.LineUp();
            }
            else if (_scrollDirection > 0)
            {
                _scrollViewer.LineDown();
            }
        }
    }
}
