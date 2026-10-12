// ── HTML 预览（并排双 WebView）共享支撑 ──────────────────────────────────────
// 供「容器内 HTML 文档」类对比视图插件复用（CHM / EPUB，以及后续 HTML / Vue 预览插件）：
// 探测宿主进程里是否有可用的原生 WebView 引擎，并提供并排双 WebView 预览控件与
// HTML 内嵌资源内联器。
//
// 引擎 = 官方 Avalonia.Controls.WebView（NativeWebView），与宿主 UI/WpfCompat/WebView2Stub.cs
// 用的是同一份程序集（宿主共享程序集，插件不随包分发），探测口径也一致：
//   Windows=WebView2 / macOS=WKWebView / Linux=WPE WebKit（缺失回退 WebKitGTK）。
// 两者都以 WebViewAdapterInfo.GetAdapterInfo(type).IsSupported && IsInstalled 判定；
// 探测不到时（headless 测试环境、Windows 未装 WebView2 运行时、Linux 未装 WPE/WebKitGTK）
// 不渲染页面，由控件显示提示文案。
using System;
using System.Reflection;
using Avalonia.Platform;
using WindowsWebView2EnvironmentRequestedEventArgs = Avalonia.Platform.WindowsWebView2EnvironmentRequestedEventArgs;

namespace ForkPlus.Plugins.Ui.HtmlPreview
{
	/// <summary>HTML 预览原生 WebView 的可用性探测（进程级缓存）与共享配置。</summary>
	public static class HtmlWebViewSupport
	{
		private static bool? _available;
		private static string _reason;

		/// <summary>WebView2（仅 Windows）用户数据目录；宿主可覆盖，未设置则用默认目录。
		/// 不设置时 WebView2 运行时会把用户数据落在进程可执行文件旁，需显式固定。</summary>
		public static string UserDataFolder { get; set; }

		/// <summary>当前环境是否有可用的原生 WebView 引擎（进程级缓存）。</summary>
		public static bool IsAvailable
		{
			get
			{
				if (!_available.HasValue)
				{
					_available = Probe(out _reason);
				}
				return _available.Value;
			}
		}

		/// <summary>不可用原因（用于提示文案）；可用时为空字符串。</summary>
		public static string UnavailableReason
		{
			get
			{
				_ = IsAvailable; // 触发探测
				return _reason ?? string.Empty;
			}
		}

		/// <summary>EnvironmentRequested 处理：Windows 平台把 WebView2 用户数据目录固定到
		/// 非可执行文件目录，避免运行时在程序目录落盘；非 Windows 平台参数类型不同，模式匹配
		/// 不命中即无副作用。</summary>
		public static void ConfigureEnvironment(object environmentArgs)
		{
			if (environmentArgs is WindowsWebView2EnvironmentRequestedEventArgs wv2Args)
			{
				wv2Args.UserDataFolder = ResolveUserDataFolder();
			}
		}

		/// <summary>解析 WebView2 用户数据目录：宿主显式设置优先，否则回落本地应用数据目录。</summary>
		public static string ResolveUserDataFolder()
		{
			if (!string.IsNullOrEmpty(UserDataFolder))
			{
				return UserDataFolder;
			}
			try
			{
				string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				if (string.IsNullOrEmpty(baseDir))
				{
					baseDir = System.IO.Path.GetTempPath();
				}
				return System.IO.Path.Combine(baseDir, "ForkPlus", "WebView2");
			}
			catch
			{
				return null;
			}
		}

		private static bool Probe(out string reason)
		{
			reason = string.Empty;
			try
			{
				if (IsHeadlessRuntime())
				{
					reason = "headless";
					return false;
				}
				if (OperatingSystem.IsWindows())
				{
					if (AdapterUsable(WebViewAdapterType.WebView2))
					{
						return true;
					}
					reason = "Windows 未安装 WebView2 运行时";
					return false;
				}
				if (OperatingSystem.IsMacOS())
				{
					if (AdapterUsable(WebViewAdapterType.WkWebView))
					{
						return true;
					}
					reason = "macOS WKWebView 不可用";
					return false;
				}
				if (OperatingSystem.IsLinux())
				{
					// WPE 优先（离屏合成进视觉树），未装再试 WebKitGTK
					if (AdapterUsable(WebViewAdapterType.WpeWebKit) || AdapterUsable(WebViewAdapterType.WebKitGtk))
					{
						return true;
					}
					reason = "Linux 未安装 WPE WebKit / WebKitGTK";
					return false;
				}
				reason = "当前平台不支持 WebView";
				return false;
			}
			catch (Exception ex)
			{
				reason = ex.Message;
				return false;
			}
		}

		/// <summary>headless 运行时探测：测试宿主（testhost* / *Tests）作为入口进程。
		/// 与宿主 WebView2Stub 同口径——单测/截图证据链走非 WebView 路径。
		/// 正式运行（ForkPlus 作为入口）恒 false。</summary>
		private static bool IsHeadlessRuntime()
		{
			try
			{
				string entry = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
				return entry.StartsWith("testhost", StringComparison.OrdinalIgnoreCase)
					|| entry.IndexOf(".Tests", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			catch
			{
				return true;
			}
		}

		private static bool AdapterUsable(WebViewAdapterType type)
		{
			try
			{
				var info = WebViewAdapterInfo.GetAdapterInfo(type);
				return info != null && info.IsSupported && info.IsInstalled;
			}
			catch
			{
				return false;
			}
		}
	}
}
