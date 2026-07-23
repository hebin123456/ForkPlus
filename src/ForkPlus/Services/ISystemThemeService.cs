using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 系统主题检测抽象（替换 <see cref="ForkPlus.App"/> 中读取 Windows 注册表
	/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize</c> 和
	/// <c>HKCU\Software\Microsoft\Windows\DWM</c> 的逻辑）。
	/// </summary>
	public interface ISystemThemeService
	{
		/// <summary>当前系统明暗主题（仅明暗二元）。</summary>
		SystemTheme CurrentSystemTheme { get; }

		/// <summary>系统是否启用了强调色全屏应用（DWM ColorPrevalence）。</summary>
		bool IsSystemAccentBrushEnabled { get; }

		/// <summary>订阅系统主题变更事件（Windows: UISettings.ColorValuesChanged）。</summary>
		void SubscribeToSystemEvents();

		/// <summary>取消订阅。</summary>
		void UnsubscribeFromSystemEvents();
	}

	/// <summary>系统明暗主题枚举。</summary>
	public enum SystemTheme
	{
		Light = 0,
		Dark = 1
	}
}
