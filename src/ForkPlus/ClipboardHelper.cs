using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace ForkPlus
{
	public static class ClipboardHelper
	{
		[Null]
		public static string GetText()
		{
			try
			{
				return Clipboard.GetData(DataFormats.Text) as string;
			}
			catch
			{
				return null;
			}
		}

		public static void SetText(string text)
		{
			Exception exception = null;
			for (int i = 0; i < 6; i++)
			{
				try
				{
					Clipboard.SetDataObject(text ?? "", copy: true);
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
				Clipboard.SetText(text ?? "");
				return;
			}
			catch (Exception ex3)
			{
				exception = ex3;
			}
			Log.Error("Failed to copy text to clipboard", exception);
			LogProcessLockingClipboard();
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
