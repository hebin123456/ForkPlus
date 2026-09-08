using System;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	public class HeaderMenuItem : MenuItem
	{
		protected override Type StyleKeyOverride => typeof(MenuItem);

		public HeaderMenuItem(string title)
		{
			// 作为“只读分组标题”展示：必须可见但不可交互。
			// Avalonia 在某些菜单主题/平台下 Disabled 的 MenuItem 可能会变得不明显；
			// 因此保持 Enabled，但禁用命中测试 + 禁用聚焦，达到 WPF HeaderMenuItem 的效果。
			base.Header = PreferencesLocalization.MenuHeader(title);
			base.Focusable = false;
			base.IsHitTestVisible = false;
			if (Application.Current?.TryFindResource("Menu.MenuItem.Disabled.Foreground", out var brush) == true && brush is IBrush b)
			{
				base.Foreground = b;
			}
		}
	}
}
