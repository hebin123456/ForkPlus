using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 平台无关的 Toast 通知服务接口。
	/// 阶段 6 跨平台化：原接口 Show(string xmlPayload) 接收 Windows Toast XML，
	/// 已重构为 Show(ToastPayload payload)，由各平台实现负责转换为原生通知格式：
	/// - Windows：ToastPayload → Toast XML → WinRT ToastNotificationManager
	/// - Linux：ToastPayload → notify-send 命令
	/// - macOS：ToastPayload → osascript display notification
	/// </summary>
	public interface IToastNotificationService
	{
		void Show(ToastPayload payload);
	}

	/// <summary>
	/// 平台无关的 Toast 通知数据。
	/// 替代原 Windows Toast XML 字符串，便于跨平台实现统一解析。
	/// </summary>
	public sealed class ToastPayload
	{
		/// <summary>通知标题（粗体显示，单行）。</summary>
		public string Title { get; }

		/// <summary>通知正文（可多行）。</summary>
		public string Body { get; }

		/// <summary>
		/// 点击通知时的启动参数（可选）。
		/// Windows：填入 toast@launch 属性，由 ToastNotificationManagerCompat.OnActivated 回调接收。
		/// Linux/macOS：当前不支持点击回调，参数仅记录到日志。
		/// </summary>
		[Null]
		public string LaunchArgument { get; }

		/// <summary>
		/// 通知附带的图片 URL（可选，仅 Windows 支持 hero 图片）。
		/// Linux/macOS 忽略此字段。
		/// </summary>
		[Null]
		public string ImageUrl { get; }

		/// <summary>true 表示静默通知（不播放声音）；false 使用系统默认提示音。</summary>
		public bool Silent { get; }

		public ToastPayload(string title, string body,
			[Null] string launchArgument = null,
			[Null] string imageUrl = null,
			bool silent = true)
		{
			Title = title ?? string.Empty;
			Body = body ?? string.Empty;
			LaunchArgument = launchArgument;
			ImageUrl = imageUrl;
			Silent = silent;
		}
	}
}
