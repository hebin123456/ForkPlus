// 跨平台系统主题监听。
// Windows: 仍使用 WinRT UISettings（系统主题变化监听）
// macOS/Linux: 通过 Avalonia Application.ActualThemeVariantChanged 监听主题变化。
//
// 系统强调色获取已迁移到 AvaloniaThemeService，通过 Avalonia 11.3 官方跨平台 API
// Application.Current.PlatformSettings.GetColorValues().AccentColor1 实现：
//   - macOS：后端自动读 NSColor.controlAccentColor
//   - Linux X11：后端读 GTK 主题
//   - Windows：后端读 WinRT UISettings
// 本类仅负责"系统主题变化"事件订阅，不再提供 GetSystemBrush（已删除，无调用方）。
//
// 注：WinRT API 仅在 Windows 平台可用，运行时按 OperatingSystem.IsWindows() 分支调用，
//     非 Windows 平台不加载 WinRT 类型，避免 TypeLoadException。
using System;
using System.Reflection;
using Avalonia;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.UI
{
	internal static class SystemThemeHelper
	{
		[Null]
		private static object _uiSettings;

		public static void SubscribeToSystemEvents()
		{
			if (OperatingSystem.IsWindows())
			{
				SubscribeWindows();
			}
			else
			{
				SubscribeUnix();
			}
		}

		// Windows 平台仍走 WinRT UISettings（系统主题变化监听）。
		// 用反射延迟加载 WinRT 类型，避免在非 Windows 平台编译/加载时引发 TypeLoadException。
		private static void SubscribeWindows()
		{
			try
			{
				Type uiSettingsType = Type.GetType("Windows.UI.ViewManagement.UISettings, Windows.UI.ViewManagement, ContentType=WindowsRuntime", throwOnError: false);
				if (uiSettingsType == null)
				{
					SubscribeUnix();
					return;
				}
				object uiSettings = Activator.CreateInstance(uiSettingsType);
				EventInfo colorValuesChangedEvent = uiSettingsType.GetEvent("ColorValuesChanged");
				if (colorValuesChangedEvent != null)
				{
					// 注册一个仅记日志 + 刷新主题的事件处理器。
					EventHandler<object> handler = (s, a) =>
					{
						Log.Info("System colors changed");
						Dispatcher.UIThread.Post(() => Theme.Refresh());
					};
					colorValuesChangedEvent.AddEventHandler(uiSettings, handler);
				}
				_uiSettings = uiSettings;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to subscribe WinRT UISettings, fallback to Avalonia theme variant", ex);
				SubscribeUnix();
			}
		}

		// 非 Windows 平台通过 Avalonia Application.ActualThemeVariantChanged 监听主题变化。
		// 系统主题切换时 Avalonia 会自动更新 ActualThemeVariant（依赖系统主题源），此处仅做转发。
		private static void SubscribeUnix()
		{
			try
			{
				Application app = Application.Current;
				if (app == null)
				{
					return;
				}
				app.ActualThemeVariantChanged += OnActualThemeVariantChanged;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to subscribe Avalonia ActualThemeVariantChanged", ex);
			}
		}

		private static void OnActualThemeVariantChanged(object sender, EventArgs e)
		{
			Log.Info("System theme variant changed");
			Dispatcher.UIThread.Post(() => Theme.Refresh());
		}
	}
}
