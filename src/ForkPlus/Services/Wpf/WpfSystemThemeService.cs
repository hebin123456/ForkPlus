using ForkPlus.Settings;
using ForkPlus.UI;
using Microsoft.Win32;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// Windows 平台的 <see cref="ISystemThemeService"/> 实现。
	/// 读取注册表 <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize</c>
	/// 和 <c>HKCU\Software\Microsoft\Windows\DWM</c>，与现有 <see cref="ForkPlus.App"/> 私有逻辑一致。
	/// 订阅委托给 <see cref="SystemThemeHelper"/>（WinRT UISettings.ColorValuesChanged）。
	/// </summary>
	public class WpfSystemThemeService : ISystemThemeService
	{
		public SystemTheme CurrentSystemTheme
		{
			get
			{
				try
				{
					using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
						"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))
					{
						object value = key?.GetValue("AppsUseLightTheme");
						if (value != null)
						{
							return ((int)value <= 0) ? SystemTheme.Dark : SystemTheme.Light;
						}
					}
					// 注册表读不到时回退到用户设置的主题
					return (ForkPlusSettings.Default.Theme != 0) ? SystemTheme.Dark : SystemTheme.Light;
				}
				catch (System.Exception ex)
				{
					Log.Error("Failed to read system theme from Windows registry", ex);
					return SystemTheme.Light;
				}
			}
		}

		public bool IsSystemAccentBrushEnabled
		{
			get
			{
				try
				{
					using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\DWM"))
					{
						object value = key?.GetValue("ColorPrevalence");
						if (value != null)
						{
							return (int)value > 0;
						}
					}
					return false;
				}
				catch (System.Exception ex)
				{
					Log.Error("Failed to read system accent brush setting from registry", ex);
					return false;
				}
			}
		}

		public void SubscribeToSystemEvents()
		{
			SystemThemeHelper.SubscribeToSystemEvents();
		}

		public void UnsubscribeFromSystemEvents()
		{
			// 现有 SystemThemeHelper 未暴露 Unsubscribe；阶段 0 暂为空实现，
			// 阶段 5 跨平台化时补充（WinRT UISettings 事件解绑）。
		}
	}
}
