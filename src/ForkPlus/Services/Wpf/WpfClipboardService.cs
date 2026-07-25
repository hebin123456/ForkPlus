// 阶段 4.5：WPF→Avalonia 迁移（WPF 兼容层实现，保留类名/命名空间）。
// - System.Windows.Clipboard → Avalonia TopLevel.Clipboard（动态类型，避免 IClipboard 命名空间歧义）
// - IClipboardService 为同步接口，Avalonia 剪贴板为异步：用 GetAwaiter().GetResult() 阻塞桥接
// - 保留原始 6 次重试 + Win32 GetOpenClipboardWindow 进程诊断逻辑（业务逻辑不变）
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace ForkPlus.Services.Wpf
{
	public class WpfClipboardService : IClipboardService
	{
		public void SetText(string text)
		{
			Exception exception = null;
			text = text ?? "";
			dynamic clipboard = GetClipboard();
			for (int i = 0; i < 6; i++)
			{
				try
				{
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
				dynamic clipboard = GetClipboard();
				return clipboard != null ? (string)clipboard.GetTextAsync().GetAwaiter().GetResult() : null;
			}
			catch
			{
				return null;
			}
		}

		private static object GetClipboard()
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
