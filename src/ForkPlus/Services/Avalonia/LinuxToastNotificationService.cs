using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using ForkPlus.Services;

namespace ForkPlus.Services.Avalonia
{
	/// <summary>
	/// 阶段 6：Linux 平台的 Toast 通知服务，封装 FreeDesktop notify-send 工具。
	/// notify-send 是 freedesktop.org 规范的工具，在 GNOME/KDE/XFCE/Cinnamon/MATE 等
	/// 主流桌面环境中默认安装（libnotify 包）。缺失时静默降级（仅记日志）。
	///
	/// 与 Windows Toast 的差异：
	/// - 无 launch/click 回调（notify-send 不支持点击回调，需要 DBus 接口才能实现）
	/// - 无 hero 图片（notify-send 支持 --icon 但仅图标，非 hero 大图）
	/// - 静默属性由 notify-send 默认行为决定（系统通知设置控制声音）
	/// </summary>
	[SupportedOSPlatform("linux")]
	public class LinuxToastNotificationService : IToastNotificationService
	{
		public void Show(ToastPayload payload)
		{
			if (payload == null)
			{
				return;
			}
			try
			{
				// notify-send [--icon=icon-name] [--urgency=normal] [--app-name=ForkPlus] title body
				// urgency: low/normal/critical（normal 默认）
				// --app-name 自 libnotify 0.7.6 起支持，缺失时会被忽略，不影响通知显示。
				var psi = new ProcessStartInfo
				{
					FileName = "notify-send",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardError = true,
					RedirectStandardOutput = true,
				};
				psi.ArgumentList.Add("--app-name=ForkPlus");
				// 静默通知不传 --urgency=critical（critical 会绕过勿扰模式，与 silent 语义相反）。
				psi.ArgumentList.Add("--urgency=normal");
				// title 与 body 作为位置参数（ArgumentList 自动转义，无需手动引号）。
				// 空字符串也要传，否则 notify-send 报参数不足。
				psi.ArgumentList.Add(payload.Title ?? string.Empty);
				psi.ArgumentList.Add(payload.Body ?? string.Empty);

				using (var proc = Process.Start(psi))
				{
					if (proc != null)
					{
						// 通知发送是 fire-and-forget，但等待 2 秒以让 notify-send 完成参数解析；
						// 若 notify-send 不存在会立即退出（ExitCode != 0），日志记录便于诊断。
						if (!proc.WaitForExit(2000))
						{
							Log.Debug("notify-send did not exit within 2s, notification may still be queued");
						}
						else if (proc.ExitCode != 0)
						{
							string stderr = proc.StandardError.ReadToEnd();
							Log.Debug("notify-send exited " + proc.ExitCode + ": " + stderr.Trim());
						}
					}
				}
			}
			catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
			{
				// notify-send 未安装或不在 PATH（FileNotFound）。
				Log.Info("notify-send unavailable, toast notification skipped: " + ex.Message);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show Linux toast notification", ex);
			}
		}
	}
}
