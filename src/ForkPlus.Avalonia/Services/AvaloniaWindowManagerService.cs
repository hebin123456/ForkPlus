using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    /// <summary>
    /// IWindowManagerService 的 Avalonia 实现。
    ///
    /// 对照 WPF 实现 src/ForkPlus/Services/Wpf/WpfWindowManagerService.cs（44 行）：
    /// - ActivateAndShowNotifications → 取 Avalonia 主窗口，调用 Activate()
    ///   （Avalonia 工程暂未集成 NotificationManager 面板，spike 阶段只激活窗口）
    /// - TryActivateWindowByTitle → 遍历 Application.Current.ApplicationWindows，
    ///   按 Title 字段匹配激活
    /// - DispatchToUiThread → Avalonia.Threading.Dispatcher.UIThread.Post(action)
    ///
    /// 调用方：ForkPlus.Core.Accounts.NotificationManager（仅 2 处，Toast 回调）。
    /// </summary>
    public class AvaloniaWindowManagerService : IWindowManagerService
    {
        /// <summary>激活主窗口并显示通知管理器面板。
        /// Avalonia 工程暂未集成通知面板（spike），仅激活主窗口。</summary>
        public void ActivateAndShowNotifications()
        {
            Window mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                mainWindow.Activate();
                if (mainWindow.WindowState == WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Normal;
                }
            }
        }

        /// <summary>按标题查找并激活某个窗口（用于 AI 代码审查窗口的去重激活）。
        /// 遍历 Avalonia Application.Current.ApplicationWindows 找到 Title 匹配的窗口。</summary>
        public bool TryActivateWindowByTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return false;
            }
            foreach (Window window in GetOpenWindows())
            {
                if (string.Equals(window.Title, title, StringComparison.Ordinal))
                {
                    window.Activate();
                    if (window.WindowState == WindowState.Minimized)
                    {
                        window.WindowState = WindowState.Normal;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>把指定 action 派发到 Avalonia UI 线程异步执行。
        /// 对照 WPF 的 Application.Current.Dispatcher.Async(action)。</summary>
        public void DispatchToUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }
            Dispatcher.UIThread.Post(action);
        }

        /// <summary>获取当前 Avalonia 桌面应用的主窗口。
        /// 兼容 IClassicDesktopStyleApplicationLifetime（桌面），
        /// 不支持 mobile/browser 生命周期（本应用只跑桌面）。</summary>
        private static Window GetMainWindow()
        {
            return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        }

        /// <summary>获取当前应用打开的所有窗口。
        /// 对照 WPF 的 Application.Current.Windows（WindowCollection）。
        /// Avalonia 11 的 IClassicDesktopStyleApplicationLifetime.Windows 是 IReadOnlyList&lt;Window&gt;。</summary>
        private static System.Collections.Generic.IReadOnlyList<Window> GetOpenWindows()
        {
            IClassicDesktopStyleApplicationLifetime desktop =
                Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            return desktop?.Windows ?? Array.Empty<Window>();
        }
    }
}
