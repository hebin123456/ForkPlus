using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// Windows 平台的 Toast 通知服务，封装 WinRT Toast API。
	/// 阶段 6 跨平台化：原 Show(string xmlPayload) 改为 Show(ToastPayload payload)，
	/// 内部由 BuildToastXml 将结构化数据转换为 Windows Toast XML，再走 WinRT。
	/// 非 Windows 平台不会被实例化（App.axaml.cs 按 OperatingSystem.IsXxx() 选择实现）。
	///
	/// 阶段 6 修复（Windows 启动崩溃）：
	/// 直接引用 Windows.Data.Xml.Dom / Windows.UI.Notifications 类型在某些 Windows SDK
	/// 版本 / NativeAOT publish 下会因类型加载失败导致运行时崩溃。
	/// 改为反射延迟加载 WinRT 类型，与跨平台重构前的原始实现一致：
	///   - Type.GetType("..., ContentType=WindowsRuntime") 按 OS 版本动态解析
	///   - 失败时静默降级（记日志），不抛异常
	///   - [UnconditionalSuppressMessage] 抑制 AOT trim 警告（反射调用无法静态分析）
	/// </summary>
	public class WpfToastNotificationService : IToastNotificationService
	{
		/// <summary>
		/// Toast 通知的 AppUserModelID，必须与 App 静态构造中 SetCurrentProcessExplicitAppUserModelID
		/// 设置的 AUMID 一致，否则通知无法与进程关联（任务栏分组 + 点击激活）。
		/// </summary>
		private const string AppUserModelId = "com.squirrel.ForkPlus.ForkPlus";

		[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
			Justification = "WinRT Toast API 通过反射延迟加载，类型由 Windows SDK 运行时提供，不参与 AOT trim 分析。")]
		[UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
			Justification = "WinRT Toast API 的反射调用不依赖动态代码生成，Activator.CreateInstance 对 WinRT 投影类型有效。")]
		public void Show(ToastPayload payload)
		{
			if (payload == null)
			{
				return;
			}
#if WINDOWS
			if (!OperatingSystem.IsWindows())
			{
				return;
			}
			try
			{
				string xml = BuildToastXml(payload);
				ShowViaWinRT(xml);
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

#if WINDOWS
		/// <summary>
		/// 通过反射延迟加载 WinRT Toast 类型并发送通知。
		/// 反射调用避免编译期对 Windows.Data.Xml.Dom / Windows.UI.Notifications 的硬引用，
		/// 在不同 Windows SDK 版本 / NativeAOT publish 下都能优雅降级。
		/// 任意一步失败均记日志返回，不向上抛异常（通知失败不应影响主流程）。
		/// </summary>
		[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
			Justification = "WinRT 类型通过 Type.GetType 反射加载，运行时由 Windows SDK 提供。")]
		[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075:DoesNotSatisfyDynamicallyAccessedMembers",
			Justification = "WinRT 投影类型的方法句柄在运行时由 CLR 解析，不参与静态 trim 分析。")]
		private static void ShowViaWinRT(string xml)
		{
			// 1. 加载 Windows.Data.Xml.Dom.XmlDocument 并 LoadXml
			Type xmlDocType = Type.GetType("Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom, ContentType=WindowsRuntime", throwOnError: false);
			if (xmlDocType == null)
			{
				Log.Info("WinRT XmlDocument unavailable, toast notification skipped");
				return;
			}
			object document = Activator.CreateInstance(xmlDocType);
			MethodInfo loadXmlMethod = xmlDocType.GetMethod("LoadXml", new[] { typeof(string) });
			if (loadXmlMethod == null || document == null)
			{
				Log.Info("WinRT XmlDocument.LoadXml unavailable, toast notification skipped");
				return;
			}
			loadXmlMethod.Invoke(document, new object[] { xml });

			// 2. 获取 ToastNotificationManager 默认实例并创建 ToastNotifier
			Type managerType = Type.GetType("Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime", throwOnError: false);
			if (managerType == null)
			{
				Log.Info("WinRT ToastNotificationManager unavailable, toast notification skipped");
				return;
			}
			// 优先用 Default 静态属性（Win10 19041+），不存在则回退到 GetForCurrentApplication()
			object defaultManager = null;
			PropertyInfo defaultProp = managerType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
			if (defaultProp != null)
			{
				defaultManager = defaultProp.GetValue(null);
			}
			if (defaultManager == null)
			{
				MethodInfo getCurrentMethod = managerType.GetMethod("GetForCurrentApplication", Type.EmptyTypes);
				if (getCurrentMethod != null)
				{
					defaultManager = getCurrentMethod.Invoke(null, null);
				}
			}
			if (defaultManager == null)
			{
				Log.Info("WinRT ToastNotificationManager default instance unavailable, toast notification skipped");
				return;
			}

			// CreateToastNotifier(string applicationId) 需要传 AUMID
			MethodInfo createNotifierMethod = defaultManager.GetType().GetMethod("CreateToastNotifier", new[] { typeof(string) });
			if (createNotifierMethod == null)
			{
				// 回退到无参重载（使用进程默认 AUMID）
				createNotifierMethod = defaultManager.GetType().GetMethod("CreateToastNotifier", Type.EmptyTypes);
				if (createNotifierMethod == null)
				{
					Log.Info("WinRT CreateToastNotifier unavailable, toast notification skipped");
					return;
				}
				object notifier = createNotifierMethod.Invoke(defaultManager, null);
				ShowNotification(notifier, document);
				return;
			}
			object notifierWithAumid = createNotifierMethod.Invoke(defaultManager, new object[] { AppUserModelId });
			ShowNotification(notifierWithAumid, document);
		}

		[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
			Justification = "WinRT ToastNotification 类型通过反射加载，运行时由 Windows SDK 提供。")]
		private static void ShowNotification(object notifier, object document)
		{
			if (notifier == null || document == null)
			{
				return;
			}
			Type toastType = Type.GetType("Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType=WindowsRuntime", throwOnError: false);
			if (toastType == null)
			{
				Log.Info("WinRT ToastNotification type unavailable, toast notification skipped");
				return;
			}
			// ToastNotification 构造函数接收 XmlDocument 参数
			ConstructorInfo ctor = toastType.GetConstructor(new[] { document.GetType() });
			if (ctor == null)
			{
				// 回退：用基类 XmlDocument 接口类型查找构造函数
				ConstructorInfo ifaceCtor = toastType.GetConstructor(new[] { typeof(object) });
				if (ifaceCtor == null)
				{
					Log.Info("WinRT ToastNotification constructor unavailable, toast notification skipped");
					return;
				}
				object notification = ifaceCtor.Invoke(new object[] { document });
				InvokeShow(notifier, notification);
				return;
			}
			object toastNotification = ctor.Invoke(new object[] { document });
			InvokeShow(notifier, toastNotification);
		}

		private static void InvokeShow(object notifier, object notification)
		{
			if (notifier == null || notification == null)
			{
				return;
			}
			MethodInfo showMethod = notifier.GetType().GetMethod("Show", new[] { notification.GetType() });
			if (showMethod == null)
			{
				// 回退：用基类 ToastNotification 类型查找 Show 方法
				MethodInfo fallbackShow = notifier.GetType().GetMethod("Show");
				if (fallbackShow == null)
				{
					Log.Info("WinRT ToastNotifier.Show unavailable, toast notification skipped");
					return;
				}
				fallbackShow.Invoke(notifier, new object[] { notification });
				return;
			}
			showMethod.Invoke(notifier, new object[] { notification });
		}
#endif

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
