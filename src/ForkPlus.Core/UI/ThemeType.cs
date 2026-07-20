namespace ForkPlus.UI
{
	/// <summary>预设皮肤类型。值 0(Light)/1(Dark) 保持不变以兼容旧 settings.json；
	/// 新增皮肤追加到末尾。每个皮肤通过 <see cref="ThemeTypeExtensions.IsDarkBase"/> 标记
	/// 其基底明暗（用于 WebView2、系统跟随等只能二选一的场景）。</summary>
	public enum ThemeType
	{
		Light = 0,
		Dark = 1,
		SolarizedLight = 2,
		SolarizedDark = 3,
		Dracula = 4,
		GitHubLight = 5,
		GitHubDark = 6,
		Monokai = 7,
		PurpleLight = 8,
		PurpleDark = 9,
		GreenLight = 10,
		GreenDark = 11,
		// v3.1.1：纯色主题扩展（按彩虹色：红→橙→黄→绿→青→蓝→紫）
		RedLight = 12,
		RedDark = 13,
		OrangeLight = 14,
		OrangeDark = 15,
		YellowLight = 16,
		YellowDark = 17,
		CyanLight = 18,
		CyanDark = 19,
		BlueLight = 20,
		BlueDark = 21
	}
}
