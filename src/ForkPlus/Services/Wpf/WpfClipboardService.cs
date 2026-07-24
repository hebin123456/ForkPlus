// 阶段 4.5：WPF→Avalonia 迁移（WPF 兼容层实现，保留类名/命名空间）。
// - using System.Windows → using Avalonia（System.Windows.Clipboard 不再使用）
// - System.Windows.Clipboard.SetDataObject(text, copy:true) / SetText(text) → Avalonia IClipboard.SetTextAsync(text)
// - System.Windows.Clipboard.GetData(DataFormats.Text) → Avalonia IClipboard.GetTextAsync()
// - 剪贴板获取：任务建议 Application.Current.Clipboard 或 TopLevel.Clipboard；
//   Avalonia 规范 API 为 TopLevel.Clipboard（Application 无 Clipboard 属性），此处沿用 MainWindow.xaml.cs
//   的 lifetime 取窗口模式：(Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard
// - IClipboardService 为同步接口，Avalonia 剪贴板为异步：用 GetAwaiter().GetResult() 阻塞桥接
//   （Win32 下 SetTextAsync/GetTextAsync 通常同步完成，不会 UI 死锁）
// - 保留原始 6 次重试 + Win32 GetOpenClipboardWindow 进程诊断逻辑（业务逻辑不变）
// - COMException / ExternalException 在 System.Runtime.InteropServices（原 using 已覆盖）
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;

namespace ForkPlus.Services.Wpf
{
	public class WpfClipboardService : IClipboardService
	{
		public void SetText(string text)
		{
			Exception exception = null;
			text = text ?? "";
			// 阶段 4.5：取主窗口（TopLevel）的剪贴板。
			IClipboard clipboard = GetClipboard();
			for (int i = 0; i < 6; i++)
			{
				try
				{
					// 阶段 4.5：WPF Clipboard.SetDataObject(text, copy:true) → Avalonia IClipboard.SetTextAsync(text)。
					if (clipboard != null)
					{
						clipboard.SetTextAsync(text).GetAwaiter().GetResult();
					}
					return;
				}
				catch (COMException ex)
				{
					exception = ex;
					Thread.Sleep(20 * (i + 1));
				}
				catch (ExternalException ex2)
				{
					exception = ex2;
					Thread.Sleep(20 * (i + 1));
				}
			}
			try
			{
				// 阶段 4.5：WPF Clipboard.SetText(text) 兜底 → Avalonia IClipboard.SetTextAsync(text)（与循环体同一 API）。
				if (clipboard != null)
				{
					clipboard.SetTextAsync(text).GetAwaiter().GetResult();
				}
			}
			catch (Exception ex3)
			{
				exception = ex3;
			}
			if (exception != null)
			{
				Log.Error("Failed to copy text to clipboard", exception);
				LogProcessLockingClipboard();
			}
		}

		public string GetText()
		{
			try
			{
				// 阶段 4.5：WPF Clipboard.GetData(DataFormats.Text) → Avalonia IClipboard.GetTextAsync()。
				IClipboard clipboard = GetClipboard();
				return clipboard != null ? clipboard.GetTextAsync().GetAwaiter().GetResult() : null;
			}
			catch
			{
				return null;
			}
		}

		// 阶段 4.5：Avalonia 剪贴板通过 TopLevel.Clipboard 访问；服务层无控件引用，
		// 取主窗口（Window 派生自 TopLevel）的 Clipboard（参考 MainWindow.xaml.cs 的 lifetime 取窗口模式）。
		private static IClipboard GetClipboard()
		{
			return (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;
		}

		private static void LogProcessLockingClipboard()
		{
			try
			{
				Process processLockingClipboard = GetProcessLockingClipboard();
				if (processLockingClipboard != null)
				{
					Log.Error("Clipboard is blocked by '" + processLockingClipboard.ProcessName + "' at '" + processLockingClipboard.StartInfo.FileName + "'");
				}
				else
				{
					Log.Error("Can't find process locking clipboard");
				}
			}
			catch
			{
				Log.Error("Can't get process locking clipboard");
			}
		}

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr GetOpenClipboardWindow();

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

		private static Process GetProcessLockingClipboard()
		{
			GetWindowThreadProcessId(GetOpenClipboardWindow(), out var lpdwProcessId);
			return Process.GetProcessById(lpdwProcessId);
		}
	}
}
