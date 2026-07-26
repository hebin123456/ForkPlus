using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// Windows 平台的 Toast 通知服务，封装 WinRT Toast API。
	/// 阶段 6 跨平台化：原 Show(string xmlPayload) 改为 Show(ToastPayload payload)，
	/// 内部由 BuildToastXml 将结构化数据转换为 Windows Toast XML，再走 WinRT。
	/// 非 Windows 平台不会被实例化（App.axaml.cs 按 OperatingSystem.IsXxx() 选择实现）。
	/// </summary>
	public class WpfToastNotificationService : IToastNotificationService
	{
		public void Show(ToastPayload payload)
		{
			if (payload == null)
			{
				return;
			}
#if WINDOWS
			try
			{
				string xml = BuildToastXml(payload);
				Windows.Data.Xml.Dom.XmlDocument document = new Windows.Data.Xml.Dom.XmlDocument();
				document.LoadXml(xml);
				Windows.UI.Notifications.ToastNotifier notifier = Windows.UI.Notifications.ToastNotificationManager.GetDefault().CreateToastNotifier("com.squirrel.ForkPlus.ForkPlus");
				Windows.UI.Notifications.ToastNotification notification = new Windows.UI.Notifications.ToastNotification(document);
				notifier.Show(notification);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show toast notification", ex);
			}
#else
			// 阶段 6：net10.0 目标下 WinRT 不可用，此分支不应被执行
			// （App.axaml.cs 在非 Windows 平台会注册 LinuxToastNotificationService / MacOsToastNotificationService）。
			// 仅在错误配置时兜底记日志，避免静默吞掉通知。
			Log.Warn("WpfToastNotificationService.Show called on non-Windows TFM; payload title=" + payload.Title);
#endif
		}

		/// <summary>
		/// 将 ToastPayload 转换为 Windows Toast XML。
		/// 与原 NotificationManager 中手写的 XML 模板等价：
		/// - audio silent 属性由 payload.Silent 控制
		/// - toast@launch 属性由 payload.LaunchArgument 控制
		/// - visual/binding ToastGeneric：title 单行 hint-maxLines=1，body 多行
		/// - image placement=hero src=payload.ImageUrl（可选）
		/// </summary>
		private static string BuildToastXml(ToastPayload payload)
		{
			string launchAttr = string.IsNullOrEmpty(payload.LaunchArgument)
				? string.Empty
				: " launch=\"" + WebUtility.HtmlEncode(payload.LaunchArgument) + "\"";
			string audioAttr = payload.Silent ? "<audio silent=\"true\"/>" : string.Empty;
			string titleXml = string.IsNullOrEmpty(payload.Title)
				? string.Empty
				: "<text hint-maxLines=\"1\">" + WebUtility.HtmlEncode(payload.Title) + "</text>";
			string bodyXml = string.IsNullOrEmpty(payload.Body)
				? string.Empty
				: "<text>" + WebUtility.HtmlEncode(payload.Body) + "</text>";
			string imageXml = string.IsNullOrEmpty(payload.ImageUrl)
				? string.Empty
				: "<image placement=\"hero\" src=\"" + WebUtility.HtmlEncode(payload.ImageUrl) + "\" />";
			return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<toast" + launchAttr + ">\n" +
			       audioAttr + "\n<visual>\n    <binding template=\"ToastGeneric\">\n        " +
			       titleXml + "\n        " + bodyXml + "\n        " + imageXml + "\n    </binding>\n</visual>\n</toast>\n";
		}
	}
}
