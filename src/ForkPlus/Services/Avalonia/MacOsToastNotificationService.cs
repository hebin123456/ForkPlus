using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using ForkPlus.Services;

namespace ForkPlus.Services.Avalonia
{
	/// <summary>
	/// 阶段 6：macOS 平台的 Toast 通知服务，封装 osascript 调用 NSUserNotification。
	/// macOS 通知中心（Notification Center）通过 NSUserNotification API 实现，
	/// 命令行可用 osascript -e 'display notification "body" with title "title"' 触发。
	///
	/// 与 Windows Toast 的差异：
	/// - 无 launch/click 回调（osascript display notification 不支持 click action）
	/// - 无 hero 图片
	/// - 静默属性由系统通知设置控制
	/// - 首次运行需用户在系统偏好 → 通知 中授权 ForkPlus（osascript 调用的脚本解释器）
	///
	/// 字符串转义：AppleScript 用 \" 转义双引号，\\ 转义反斜杠；
	/// 其他特殊字符（含换行）保留原样，AppleScript 字符串支持多行。
	/// </summary>
	[SupportedOSPlatform("macos")]
	public class MacOsToastNotificationService : IToastNotificationService
	{
		public void Show(ToastPayload payload)
		{
			if (payload == null)
			{
				return;
			}
			try
			{
				// display notification "body" with title "title" [sound name "..."]
				// Silent=true 时不带 sound（与 Windows <audio silent="true"/> 等价）。
				// Silent=false 时带 sound name "default"（macOS 默认提示音 "Funk"）。
				string title = EscapeAppleString(payload.Title);
				string body = EscapeAppleString(payload.Body);
				string script = "display notification \"" + body + "\" with title \"" + title + "\"";
				if (!payload.Silent)
				{
					script += " sound name \"default\"";
				}

				var psi = new ProcessStartInfo
				{
					FileName = "osascript",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
				};
				psi.ArgumentList.Add("-e");
				psi.ArgumentList.Add(script);

				using (var proc = Process.Start(psi))
				{
					if (proc != null)
					{
						// osascript 通常 <100ms 完成（仅投递通知到通知中心，不等用户交互）。
						if (!proc.WaitForExit(2000))
						{
							Log.Debug("osascript did not exit within 2s, notification may still be queued");
						}
						else if (proc.ExitCode != 0)
						{
							string stderr = proc.StandardError.ReadToEnd();
							// 用户未授权通知权限时 osascript 不会报错，仅静默不显示；
							// 这里仅记录实际执行错误（语法错误等）。
							Log.Debug("osascript exited " + proc.ExitCode + ": " + stderr.Trim());
						}
					}
				}
			}
			catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
			{
				// osascript 未找到（非 macOS 或环境异常）。
				Log.Info("osascript unavailable, toast notification skipped: " + ex.Message);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show macOS toast notification", ex);
			}
		}

		/// <summary>
		/// 转义 AppleScript 字符串中的特殊字符。
		/// 仅转义双引号和反斜杠（AppleScript 仅这两个字符需要转义）。
		/// 换行/制表符等保留原样（AppleScript 字符串支持多行字面量）。
		/// </summary>
		private static string EscapeAppleString(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return string.Empty;
			}
			var sb = new StringBuilder(s.Length);
			foreach (char c in s)
			{
				if (c == '"')
				{
					sb.Append("\\\"");
				}
				else if (c == '\\')
				{
					sb.Append("\\\\");
				}
				else
				{
					sb.Append(c);
				}
			}
			return sb.ToString();
		}
	}
}
