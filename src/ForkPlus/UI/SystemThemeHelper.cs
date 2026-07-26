// 阶段 5：跨平台系统主题监听。
// Windows: 仍使用 WinRT UISettings（系统强调色 / 主题变化监听）
// macOS/Linux: 通过 Avalonia Application.ActualThemeVariantChanged 监听主题变化，
//              不提供系统强调色（返回固定画刷）；强调色需求由 ThemeService 统一管理。
// 注：WinRT API 仅在 Windows 平台可用，运行时按 OperatingSystem.IsWindows() 分支调用，
//     非 Windows 平台不加载 WinRT 类型，避免 TypeLoadException。
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.UI
{
	internal static class SystemThemeHelper
	{
		[Null]
		private static object _uiSettings;

		[Null]
		private static IDisposable _themeVariantSubscription;

		private static bool IsWindows11 => App.OSVersion.Build >= 20000;

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

		// 阶段 5：Windows 平台仍走 WinRT UISettings（系统强调色 / 主题变化监听）。
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

		// 阶段 5：非 Windows 平台通过 Avalonia Application.ActualThemeVariantChanged 监听主题变化。
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

		[Null]
		public static IBrush GetSystemBrush(SystemColorType colorType)
		{
			if (OperatingSystem.IsWindows() && _uiSettings != null)
			{
				try
				{
					return GetSystemBrushWindows(colorType);
				}
				catch (Exception ex)
				{
					Log.Error("Failed to get system brush from WinRT, fallback to default", ex);
				}
			}
			// 非 Windows 平台：返回 Theme 当前强调色画刷（由主题系统统一管理）。
			return GetDefaultSystemBrush(colorType);
		}

		[Null]
		[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075",
			Justification = "WinRT 反射调用仅 Windows 平台运行（OperatingSystem.IsWindows 守卫）。WinRT 类型由 Windows 运行时加载，不受 trim/AOT 影响。非 Windows 平台不会执行到此方法。")]
		private static IBrush GetSystemBrushWindows(SystemColorType colorType)
		{
			Type uiSettingsType = _uiSettings.GetType();
			UIColorType uiColorType = ToUIColorType(colorType);
			object colorValue = uiSettingsType.GetMethod("GetColorValue")?.Invoke(_uiSettings, new object[] { (uint)uiColorType });
			if (colorValue == null)
			{
				return GetDefaultSystemBrush(colorType);
			}
			// 反射读 Windows.UI.Color 的 A/R/G/B 字段
			Type colorValueType = colorValue.GetType();
			byte a = (byte)colorValueType.GetProperty("A").GetValue(colorValue);
			byte r = (byte)colorValueType.GetProperty("R").GetValue(colorValue);
			byte g = (byte)colorValueType.GetProperty("G").GetValue(colorValue);
			byte b = (byte)colorValueType.GetProperty("B").GetValue(colorValue);
			return new SolidColorBrush(Avalonia.Media.Color.FromArgb(a, r, g, b));
		}

		// 阶段 5：非 Windows 平台或 WinRT 不可用时返回固定画刷。
		// 强调色用主题当前 Accent 画刷（已在 Theme 中配置）；其他返回透明（无系统色可用）。
		[Null]
		private static IBrush GetDefaultSystemBrush(SystemColorType colorType)
		{
			return Brushes.Transparent;
		}

		private static UIColorType ToUIColorType(SystemColorType colorType)
		{
			switch (colorType)
			{
			case SystemColorType.Accent:
			return UIColorType.Accent;
		case SystemColorType.Accent1:
			if (ForkPlusSettings.Default.Theme.IsDarkBase())
			{
				return UIColorType.AccentDark1;
			}
			return UIColorType.AccentLight1;
		case SystemColorType.Accent2:
			if (IsWindows11)
			{
				if (ForkPlusSettings.Default.Theme.IsDarkBase())
				{
					return UIColorType.AccentDark2;
				}
				return UIColorType.AccentLight1;
			}
			return UIColorType.Accent;
		default:
			return UIColorType.Accent;
		}
	}

		// 阶段 5：UIColorType 用本地枚举（避免编译时引用 WinRT），值与 Windows.UI.ViewManagement.UIColorType 一致。
		private enum UIColorType : uint
		{
			Background = 0u,
			Foreground,
			AccentDark3,
			AccentDark2,
			AccentDark1,
			Accent,
			AccentLight1,
			AccentLight2,
			AccentLight3,
			Complement
		}
	}
}
