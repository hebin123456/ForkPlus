using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ForkPlus.Services.Wpf
{
        /// <summary>
        /// Toast 通知服务（Windows 原生 Toast，Avalonia 迁移期恢复实现）。
        ///
        /// 原 WPF 版走 WinRT ToastNotificationManager（net10.0 无 WinRT 投影，包已隔离）。
        /// 迁移期降级实现此前把 Show() 做成空操作（仅记日志），导致 git mm 结束、AI Review 完成
        /// 等场景的系统原生 Toast 全部丢失（2026-09-10 用户反馈"git mm 结束后丢失系统原生通知"）。
        ///
        /// 恢复方案（不改变 net10.0 跨平台 TFM、不引入新 NuGet 包）：
        ///   1. AUMID 注册：非 MSIX 桌面应用发 Toast 必须先注册 AppUserModelID。启动时已通过
        ///      NativeMethods.SetAppUserModelID("com.squirrel.ForkPlus.ForkPlus") 给当前进程设了
        ///      AUMID（Squirrel 安装约定）；Squirrel 安装会顺带在开始菜单建带 AUMID 的快捷方式，
        ///      但开发态/免安装运行时没有该快捷方式 → Toast 不显示。这里用更轻量的注册表方案
        ///      （HKCU\Software\Classes\AppUserModelId\&lt;AUMID&gt;，微软为非 MSIX 桌面应用文档化的
        ///      替代注册路径），幂等写入 DisplayName/IconUri，无需 COM IShellLink+IPropertyStore。
        ///   2. Toast 发送：net10.0 无 WinRT 投影，不能直接 new ToastNotificationManager；
        ///      PowerShell 原生支持 WinRT 投影（[Windows.UI.Notifications.ToastNotificationManager,...]
        ///      ContentType=WindowsRuntime），故起一个 powershell.exe 子进程走 WinRT COM 发 Toast。
        ///      git mm 结束是一次性事件，子进程开销（~100ms）可接受。
        /// 非 Windows 平台 Show() 仍仅记日志（保持跨平台，无副作用）。
        /// </summary>
        public class WpfToastNotificationService : IToastNotificationService
        {
                // 与 App.AppUserModelID（private，静态构造函数里赋值 "com.squirrel.ForkPlus.ForkPlus"）保持一致。
                // 该 AUMID 是 Squirrel 安装约定的固定应用标识，启动时经 NativeMethods.SetAppUserModelID
                // 设给当前进程；Toast 发送方 AUMID 必须与之一致才能正确归属。此处直接用常量，避免改
                // App.AppUserModelID 的可见性（它本应保持 private）。
                private static readonly string AppUserModelID = "com.squirrel.ForkPlus.ForkPlus";

                private static readonly string DisplayName = App.AppName ?? "ForkPlus";

                private static bool _aumidRegistered;

                public void Show(string xmlPayload)
                {
                        if (string.IsNullOrWhiteSpace(xmlPayload))
                        {
                                return;
                        }
                        // 跨平台：非 Windows 直接降级记日志（Avalonia 迁移期保持原行为）。
                        if (!OperatingSystem.IsWindows())
                        {
                                Log.Info("Toast notification suppressed (non-Windows): " + xmlPayload.Length + " chars");
                                return;
                        }
                        try
                        {
                                EnsureAumidRegistered();
                                SendToastViaPowerShell(xmlPayload);
                        }
                        catch (Exception ex)
                        {
                                // 通知失败不应影响主流程（git mm 已执行完毕）。
                                Log.Error("Failed to show Windows toast notification", ex);
                        }
                }

                /// <summary>
                /// 幂等注册 AUMID 到 HKCU\Software\Classes\AppUserModelId\&lt;AUMID&gt;。
                /// 微软为非 MSIX 桌面应用文档化的 Toast 注册路径：写入 DisplayName（必填）与 IconUri，
                /// Windows 即认可该 AUMID 为合法 Toast 发送方。Squirrel 安装态已有开始菜单快捷方式时
                /// 此注册表项也无害（Toast 系统优先用快捷方式，注册表项作 DisplayName/IconUri 补充）。
                /// </summary>
                private static void EnsureAumidRegistered()
                {
                        if (_aumidRegistered)
                        {
                                return;
                        }
                        try
                        {
                                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                                        @"Software\Classes\AppUserModelId\" + AppUserModelID, writable: true))
                                {
                                        if (key != null)
                                        {
                                                if (key.GetValue("DisplayName") == null)
                                                {
                                                        key.SetValue("DisplayName", DisplayName, RegistryValueKind.String);
                                                }
                                                string iconUri = ResolveIconUri();
                                                if (iconUri != null && key.GetValue("IconUri") == null)
                                                {
                                                        key.SetValue("IconUri", iconUri, RegistryValueKind.String);
                                                }
                                        }
                                }
                                _aumidRegistered = true;
                        }
                        catch (Exception ex)
                        {
                                Log.Error("Failed to register AUMID '" + AppUserModelID + "' in registry", ex);
                        }
                }

                /// <summary>解析应用图标路径（app.ico，与可执行文件同目录）。</summary>
                private static string ResolveIconUri()
                {
                        try
                        {
                                string dir = AppContext.BaseDirectory;
                                string ico = Path.Combine(dir, "app.ico");
                                if (File.Exists(ico))
                                {
                                        return ico;
                                }
                        }
                        catch
                        {
                        }
                        return null;
                }

                /// <summary>
                /// 起 powershell.exe 子进程走 WinRT ToastNotificationManager 发 Toast。
                /// PowerShell 原生加载 WinRT 投影（ContentType=WindowsRuntime），net10.0 无需额外引用。
                /// </summary>
                private static void SendToastViaPowerShell(string xmlPayload)
                {
                        // XML 内含用户可见文案，已由调用方 HtmlEncode；作为参数传入需转义 PowerShell 元字符。
                        // 用 base64 编码 XML 规避所有引号/换行/特殊字符转义问题，子进程内解码。
                        string encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(xmlPayload));

                        // -ExecutionPolicy Bypass：避免企业策略阻止内联脚本。
                        // -NoProfile：跳过用户 profile 加载，加速启动并避免 profile 副作用。
                        // -WindowStyle Hidden：不弹黑窗。
                        string script =
                                "$ErrorActionPreference='Stop';" +
                                "try{" +
                                "[Windows.UI.Notifications.ToastNotificationManager,Windows.UI.Notifications,ContentType=WindowsRuntime]|Out-Null;" +
                                "[Windows.Data.Xml.Dom.XmlDocument,Windows.Data.Xml.Dom,ContentType=WindowsRuntime]|Out-Null;" +
                                "$x=New-Object Windows.Data.Xml.Dom.XmlDocument;" +
                                "$x.LoadXml([System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('" + encoded + "')));" +
                                "$t=New-Object Windows.UI.Notifications.ToastNotification $x;" +
                                "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('" + AppUserModelID + "').Show($t);" +
                                "}catch{ exit 1 };";

                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                                FileName = "powershell.exe",
                                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"" + script.Replace("\"", "\\\"") + "\"",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                RedirectStandardError = true
                        };
                        try
                        {
                                using (Process p = Process.Start(psi))
                                {
                                        // 不阻塞 UI 线程等子进程结束：通知发送是 fire-and-forget。
                                        // 但读 StandardError 需子进程退出后才有完整流；异步读取避免管道死锁。
                                        p.EnableRaisingEvents = true;
                                        string capturedError = null;
                                        p.ErrorDataReceived += (s, e) => { if (e.Data != null) capturedError = e.Data; };
                                        p.BeginErrorReadLine();
                                        // 给 PowerShell 最多 5 秒发送窗口；超时不再等待（Toast 仍可能在子进程内发出）。
                                        if (!p.WaitForExit(5000))
                                        {
                                                try { p.Kill(); } catch { }
                                                Log.Warn("Windows toast PowerShell process timed out");
                                                return;
                                        }
                                        if (p.ExitCode != 0 && !string.IsNullOrEmpty(capturedError))
                                        {
                                                Log.Warn("Windows toast PowerShell reported error: " + capturedError);
                                        }
                                }
                        }
                        catch (Exception ex)
                        {
                                Log.Error("Failed to launch PowerShell for Windows toast", ex);
                        }
                }
        }
}
