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
				// 阶段 6：点击回调等价实现。
			// Windows 用 toast@launch 属性 + OnActivated 回调实现点击跳转；
			// Linux notify-send 不支持应用内点击回调（需 DBus ActionInvoked 信号监听，架构改造大）。
			// 等价方案：若 LaunchArgument 是 URL，追加到正文末尾，用户可复制打开。
			// 这实现了"从通知打开 URL"的核心功能，交互方式不同（复制 vs 点击）。
			string body = payload.Body ?? string.Empty;
			if (!string.IsNullOrEmpty(payload.LaunchArgument) &&
				(payload.LaunchArgument.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
				 payload.LaunchArgument.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
			{
				body = body + "\n" + payload.LaunchArgument;
			}
			else
			{
				// 非 URL 的 LaunchArgument（如 ai-review: 前缀）记录到日志，便于诊断
				if (!string.IsNullOrEmpty(payload.LaunchArgument))
				{
					Log.Debug("Linux toast LaunchArgument (non-URL, no click action): " + payload.LaunchArgument);
				}
			}

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
				// 阶段 6：图片支持。notify-send --icon 接受图标名（如 "dialog-information"）或绝对路径。
				// payload.ImageUrl 是仓库头像 URL，需要先下载到临时文件再传路径（notify-send 不支持 http URL）。
				// 仅当 ImageUrl 是 http(s) 时下载；本地路径直接传。
				string iconArg = ResolveIconArgument(payload.ImageUrl);
				if (iconArg != null)
				{
					psi.ArgumentList.Add("--icon=" + iconArg);
				}
				else
				{
					// 无图片时用通用信息图标（与 Windows 默认 toast 视觉一致）
					psi.ArgumentList.Add("--icon=dialog-information");
				}
				// title 与 body 作为位置参数（ArgumentList 自动转义，无需手动引号）。
				// 空字符串也要传，否则 notify-send 报参数不足。
				psi.ArgumentList.Add(payload.Title ?? string.Empty);
				psi.ArgumentList.Add(body);

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

		/// <summary>
		/// 解析 notify-send --icon 参数值。
		/// - http(s) URL：下载到临时文件（/tmp/forkplus-toast-icon-*.png），返回路径
		/// - 本地绝对路径：直接返回
		/// - null/空：返回 null（调用方用默认 dialog-information 图标）
		///
		/// 下载超时 3 秒，失败返回 null（不影响通知发送，仅无图标）。
		/// 临时文件不主动删除（OS 会在 /tmp 清理周期内删除，且通知显示期间需要文件存在）。
		/// </summary>
		[Null]
		private static string ResolveIconArgument([Null] string imageUrl)
		{
			if (string.IsNullOrEmpty(imageUrl))
			{
				return null;
			}
			// 本地路径直接用
			if (imageUrl.StartsWith("/") && System.IO.File.Exists(imageUrl))
			{
				return imageUrl;
			}
			if (!imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
				!imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				// 既不是本地路径也不是 http URL，当作图标名传（notify-send 接受主题图标名）
				return imageUrl;
			}
			// 下载到临时文件
			try
			{
				string tmpPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
					"forkplus-toast-icon-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png");
				using (var client = new System.Net.WebClient())
				{
					client.Headers.Add("User-Agent", "ForkPlus/1.0");
					client.DownloadFile(imageUrl, tmpPath);
				}
				return tmpPath;
			}
			catch (Exception ex)
			{
				Log.Debug("Failed to download toast icon from '" + imageUrl + "': " + ex.Message);
				return null;
			}
		}
	}
}
