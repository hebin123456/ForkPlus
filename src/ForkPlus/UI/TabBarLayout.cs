namespace ForkPlus.UI
{
	/// <summary>
	/// 主窗口仓库标签条的布局：顶部（水平，传统样式）/ 左侧 / 右侧（垂直，VSCode 垂直标签风格）。
	/// 存储于 settings.json 的 "TabBarLayout" 字段（int），缺省 0 = Top（保持旧版行为）。
	/// </summary>
	public enum TabBarLayout
	{
		Top = 0,
		Left = 1,
		Right = 2
	}

	public static class TabBarLayoutExtensions
	{
		/// <summary>菜单项文案的本地化 key（PreferencesLocalization.Translate 用）。</summary>
		public static string LocalizeKey(this TabBarLayout layout)
		{
			switch (layout)
			{
				case TabBarLayout.Left:
					return "Left";
				case TabBarLayout.Right:
					return "Right";
				default:
					return "Top";
			}
		}
	}
}
