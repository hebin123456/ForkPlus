using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    // Phase 6.2：IToastNotificationService 的 Avalonia 实现。
    //
    // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfToastNotificationService.cs：
    //   WPF: 用 CommunityToolkit.WinUI.Notifications + WinRT ToastNotificationManager（Windows-only）
    //     - Show(xmlPayload): XmlDocument.LoadXml → ToastNotification →
    //       ToastNotificationManager.GetDefault().CreateToastNotifier(appId).Show()
    //     - OnActivated: 订阅 ToastNotificationManagerCompat.OnActivated，转发 e.Argument 字符串
    //
    // Avalonia 11 实现（跨平台，三平台统一，无 RuntimeInformation.IsOSPlatform 分支）：
    //   - Show(xmlPayload): 解析 WinRT toast XML 提取 title/message/launch argument，
    //     通过 Avalonia.Controls.Notifications.WindowNotificationManager.Show() 显示应用内通知
    //     （WindowNotificationManager 用主窗口 TopLevel 作为宿主，在窗口右下角渲染 NotificationCard）
    //   - OnActivated: 通过 Notification.OnClick 回调，把 XML <toast launch="..."> 属性值
    //     转发给订阅者（等价 WPF 的 e.Argument；无 launch 属性时不触发，与 WPF 行为一致）
    //
    // WindowNotificationManager 宿主挂载策略（不改 MainWindow.axaml）：
    //   - 首次 Show() 时懒加载获取主窗口 TopLevel，构造 WindowNotificationManager(topLevel)
    //   - 把主窗口原 Content（Grid）包进一个 Panel，WindowNotificationManager 作为 Panel 最后一个
    //     子元素叠加在顶层（WindowNotificationManager 内部 Panel 默认右下角对齐）
    //   - 整个初始化仅执行一次（_initialized guard），后续 Show() 复用同一个 _manager
    //
    // 不引入任何 Windows-only 包：WindowNotificationManager / Notification / NotificationType
    // 都在 Avalonia 主包的 Avalonia.Controls.Notifications 命名空间（Avalonia 11 自带，三平台统一）。
    public class AvaloniaToastNotificationService : IToastNotificationService
    {
        public event Action<string> OnActivated;

        private WindowNotificationManager _manager;
        private bool _initialized;

        public void Show(string xmlPayload)
        {
            try
            {
                EnsureInitialized();
                if (_manager == null)
                {
                    Console.WriteLine("[AvaloniaToastNotificationService] WindowNotificationManager unavailable (no main window).");
                    return;
                }

                var (title, message, argument) = ParseToastXml(xmlPayload);
                // Avalonia 11.3 Notification 仅有无参 + 6 参构造（title/message/type/expiration/onClick/onClose），
                // expiration 传 null 让 WindowNotificationManager 用默认时长。
                var notification = new Notification(title, message, NotificationType.Information,
                    expiration: null,
                    onClick: () =>
                    {
                        // 对照 WPF OnActivatedInternal：仅当 argument 非 null 时转发（与 WinRT 行为一致）
                        if (argument != null)
                        {
                            OnActivated?.Invoke(argument);
                        }
                    },
                    onClose: null);
                _manager.Show(notification);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AvaloniaToastNotificationService] Failed to show toast: {ex.Message}");
            }
        }

        // 懒加载构造 WindowNotificationManager 并注入主窗口可视树（仅一次）
        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is Window window)
            {
                var topLevel = TopLevel.GetTopLevel(window);
                if (topLevel == null) return;

                _manager = new WindowNotificationManager(topLevel);

                // 把主窗口原 Content 包进 Panel，WindowNotificationManager 作为覆盖层叠加在顶层
                if (window.Content is Control originalContent)
                {
                    var overlayPanel = new Panel();
                    window.Content = overlayPanel;
                    overlayPanel.Children.Add(originalContent);
                    overlayPanel.Children.Add(_manager);
                }
                else
                {
                    // 兜底：直接把 WindowNotificationManager 设为 Content（实际极少触发）
                    window.Content = _manager;
                }
            }
        }

        // 解析 WinRT toast XML：提取 <text> 元素作为 title/message，
        // <toast launch="..."> 属性作为点击回调 argument
        private static (string title, string message, string argument) ParseToastXml(string xmlPayload)
        {
            const string defaultTitle = "ForkPlus";
            if (string.IsNullOrWhiteSpace(xmlPayload))
                return (defaultTitle, string.Empty, null);

            try
            {
                var doc = XDocument.Parse(xmlPayload);

                string argument = doc.Descendants(XName.Get("toast"))
                    .FirstOrDefault()?.Attribute("launch")?.Value;

                var texts = new List<string>();
                foreach (var t in doc.Descendants(XName.Get("text")))
                {
                    var v = t.Value?.Trim();
                    if (!string.IsNullOrEmpty(v)) texts.Add(v);
                }

                string title = texts.Count >= 1 ? texts[0] : defaultTitle;
                string message = texts.Count >= 2 ? string.Join("\n", texts.Skip(1)) : string.Empty;
                return (title, message, argument);
            }
            catch
            {
                // XML 解析失败：把原始 payload 作为 message 显示，不触发 OnActivated
                return (defaultTitle, xmlPayload, null);
            }
        }
    }
}
