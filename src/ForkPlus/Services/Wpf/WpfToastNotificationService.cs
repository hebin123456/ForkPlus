using System;

namespace ForkPlus.Services.Wpf
{
        /// <summary>
        /// Toast 通知服务（Avalonia 迁移期降级实现）。
        /// 原 WPF 版走 WinRT ToastNotificationManager（net10.0 无投影，包已隔离）。
        /// Migration note：Windows 上可用 WebView2/PowerShell 或 MsixToolkit 方案恢复；
        /// 跨平台用社区通知库。当前仅记日志，不影响主流程。
        /// </summary>
        public class WpfToastNotificationService : IToastNotificationService
        {
                public void Show(string xmlPayload)
                {
                        Log.Info("Toast notification suppressed (Avalonia migration Migration note): " + (xmlPayload ?? string.Empty).Length + " chars");
                }
        }
}
