using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    // Phase 6.1：IClipboardService 的 Avalonia 实现。
    //
    // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfClipboardService.cs：
    //   WPF: Clipboard.SetDataObject（同步，COM 重试 6 次）+ Clipboard.GetData(Text)
    //   且 WPF 用 user32.dll P/Invoke（GetOpenClipboardWindow / GetWindowThreadProcessId）
    //   查找锁住剪贴板的进程——Windows-only，跨平台工程不引入。
    //
    // Avalonia 11 实现：
    //   - 剪贴板通过 TopLevel.Clipboard 获取（不是 Application.Clipboard）
    //   - API 为异步 SetTextAsync / GetTextAsync，但 IClipboardService 接口要求同步签名
    //   - 用 .GetAwaiter().GetResult() 同步阻塞（与 WPF 同步语义对齐）
    //
    // TopLevel 获取：从 IClassicDesktopStyleApplicationLifetime.MainWindow 取主窗口的 TopLevel。
    // 这避免了 user32.dll P/Invoke，天然跨平台（Avalonia 内部按平台分发到 win32/wayland/cocoa）。
    public class AvaloniaClipboardService : IClipboardService
    {
        public void SetText(string text)
        {
            text = text ?? "";
            var topLevel = GetTopLevel();
            var clipboard = topLevel?.Clipboard;
            if (clipboard == null) return;
            try
            {
                clipboard.SetTextAsync(text).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AvaloniaClipboardService] Failed to copy text to clipboard: {ex.Message}");
            }
        }

        public string GetText()
        {
            var topLevel = GetTopLevel();
            var clipboard = topLevel?.Clipboard;
            if (clipboard == null) return null;
            try
            {
                return clipboard.GetTextAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        private static TopLevel GetTopLevel()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return TopLevel.GetTopLevel(desktop.MainWindow);
            }
            return null;
        }
    }
}
