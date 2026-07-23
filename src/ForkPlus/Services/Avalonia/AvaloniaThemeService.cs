using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Services;

namespace ForkPlus.Services.Avalonia
{
	/// <summary>
	/// 阶段 4 里程碑 4.3：IThemeService 的 Avalonia 实现。
	/// 替换 WPF <c>Application.Current.Resources.MergedDictionaries.Add/Remove</c>
	/// 强制 <c>DynamicResource</c> 失效的机制。
	///
	/// WPF 做法：新建 ResourceDictionary，Add 到 MergedDictionaries，再 Remove 旧的，
	/// 利用 WPF 资源系统在字典增删时强制刷新所有 DynamicResource 引用的特性。
	///
	/// Avalonia 做法：直接更新 Resources 字典中的 SystemAccentBrush 键值。
	/// 主题明暗切换通过 Styles 机制（App.axaml），不依赖 MergedDictionaries。
	/// </summary>
	public class AvaloniaThemeService : IThemeService
	{
		public void Refresh()
		{
			try
			{
				var app = Application.Current;
				if (app == null)
				{
					return;
				}

				// 直接更新 Resources 中的 SystemAccentBrush（Avalonia IResourceDictionary 索引赋值）
				var accentBrush = GetSystemAccentBrush();
				app.Resources["SystemAccentBrush"] = accentBrush;
			}
			catch (Exception ex)
			{
				Log.Error("AvaloniaThemeService.Refresh failed", ex);
			}
		}

		public IBrush GetSystemBrush(SystemColorType colorType, IBrush fallback)
		{
			try
			{
				var platformSettings = Application.Current?.PlatformSettings;
				if (platformSettings == null)
				{
					return fallback;
				}

				var colorValues = platformSettings.GetColorValues();
				// Avalonia PlatformColorValues 有 AccentColor1/2/3
				var accentColor = colorType switch
				{
					SystemColorType.Accent => colorValues.AccentColor1,
					SystemColorType.Accent1 => colorValues.AccentColor1,
					SystemColorType.Accent2 => colorValues.AccentColor2,
					_ => colorValues.AccentColor1
				};
				return new SolidColorBrush(accentColor);
			}
			catch
			{
				return fallback;
			}
		}

		private static IBrush GetSystemAccentBrush()
		{
			try
			{
				var platformSettings = Application.Current?.PlatformSettings;
				if (platformSettings != null)
				{
					var accentColor = platformSettings.GetColorValues().AccentColor1;
					return new SolidColorBrush(accentColor);
				}
			}
			catch
			{
				// 忽略，返回 fallback
			}

			// fallback: 使用资源中的 AccentBrush
			if (Application.Current?.Resources.TryGetResource("AccentBrush", out object accent) == true
				&& accent is IBrush brush)
			{
				return brush;
			}
			return Brushes.Transparent;
		}
	}
}
