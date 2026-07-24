// 阶段 4.5：WPF → Avalonia 迁移。
// WPF System.Windows.Media.Brush → Avalonia.Media.IBrush。
// WPF SolidColorBrush.Freeze() → 移除（Avalonia 画刷默认不可变）。
// WPF Application.Current.Dispatcher.Invoke → Avalonia Dispatcher.UIThread.Post。
// WPF System.Windows.Media.Color → Avalonia.Media.Color。
// WinRT UISettings/ColorValuesChanged/GetColorValue/Windows.UI.Color 保留（Windows-only API）。
// TODO 阶段 5：跨平台化时替换 WinRT UISettings 为 ISystemThemeService 抽象。
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Settings;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace ForkPlus.UI
{
	internal static class SystemThemeHelper
	{
		[Null]
		private static object _uiSettings;

		private static bool IsWindows11 => App.OSVersion.Build >= 20000;

		public static void SubscribeToSystemEvents()
		{
			UISettings uiSettings = new UISettings();
			uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
			_uiSettings = uiSettings;
		}

		[Null]
		public static IBrush GetSystemBrush(Theme.SystemColorType colorType)
		{
			SolidColorBrush solidColorBrush = new SolidColorBrush(GetColor(((UISettings)_uiSettings).GetColorValue(ToUIColorType(colorType))));
			return solidColorBrush;
		}

		private static void UiSettings_ColorValuesChanged(object sender, object args)
		{
			Log.Info("System colors changed");
			Dispatcher.UIThread.Post(delegate
			{
				Theme.Refresh();
			});
		}

		private static UIColorType ToUIColorType(Theme.SystemColorType colorType)
		{
			switch (colorType)
			{
			case Theme.SystemColorType.Accent:
				return (UIColorType)5;
			case Theme.SystemColorType.Accent1:
			if (ForkPlusSettings.Default.Theme.IsDarkBase())
			{
				return (UIColorType)6;
			}
			return (UIColorType)4;
		case Theme.SystemColorType.Accent2:
			if (IsWindows11)
			{
				if (ForkPlusSettings.Default.Theme.IsDarkBase())
				{
					return (UIColorType)7;
				}
				return (UIColorType)4;
			}
			return (UIColorType)5;
		default:
			return (UIColorType)5;
		}
	}

		private static Avalonia.Media.Color GetColor(Windows.UI.Color color)
		{
			return Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
		}
	}
}
