using Avalonia.Input;
using ForkPlus.Settings;

namespace ForkPlus.UI.Commands
{
	// 阶段 4.5：WPF Application.Current.Resources.MergedDictionaries Add/Remove
	// → App.InitializeTheme (RequestedThemeVariant)。
	// WPF ResourceDictionary + Source = newTheme.ResourceUri() 在 Avalonia 中无对应操作；
	// 主题切换由 App.InitializeTheme 通过 RequestedThemeVariant 完成，并触发 Theme.Refresh()
	// + App.ApplyCustomColors()。
	public class SwitchApplicationThemeCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Switch Theme";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		/// <summary>无参切换：在基底 Light 与 Dark 之间 toggle（快捷键场景）。
		/// 当前皮肤基底为 Light 切到 Dark（反之亦然），保留用户在同类皮肤内的选择。</summary>
		public void Execute()
		{
			ThemeType current = ForkPlusSettings.Default.Theme;
			ThemeType target = current.IsDarkBase() ? ThemeType.Light : ThemeType.Dark;
			ForkPlusSettings.Default.Theme = target;
			Execute(target);
		}

		public void Execute(ThemeType newTheme, bool followSystemTheme = false)
		{
			ForkPlusSettings.Default.Theme = newTheme;
			ForkPlusSettings.Default.FollowSystemTheme = followSystemTheme;
			// 切换主题时关闭自定义颜色覆盖，使用新主题的原色（避免自定义覆盖与主题色混乱）。
			// CustomColors 字典保留，用户重新勾选"自定义颜色"时可恢复。
			ForkPlusSettings.Default.UseCustomColors = false;
			App.RefreshWindowBorderBrush();
			// 阶段 4.5：WPF MergedDictionaries Add/Remove Generic.{Skin}.xaml
			// → App.InitializeTheme 通过 RequestedThemeVariant 切换主题，
			// 内部已调用 Theme.Refresh() + App.ApplyCustomColors()。
			App.InitializeTheme();
			NotificationCenter.Current.RaiseApplicationThemeChanged(this, newTheme);
		}
	}
}
