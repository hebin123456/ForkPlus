using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using ForkPlus.Settings;

namespace ForkPlus.UI.Commands
{
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
			// 匹配任意 Generic.{SkinName}.xaml（不再写死 Light|Dark），支持多预设皮肤
			ResourceDictionary resourceDictionary = Application.Current.Resources.MergedDictionaries
				.Where((ResourceDictionary rd) => rd.Source != null)
				.FirstOrDefault((ResourceDictionary rd) => Regex.Match(rd.Source.OriginalString, @"\/ForkPlus;component\/Theme\/Generic\.\w+\.xaml").Success);
			ResourceDictionary item = new ResourceDictionary
			{
				Source = newTheme.ResourceUri()
			};
			Application.Current.Resources.MergedDictionaries.Add(item);
			if (resourceDictionary != null)
			{
				Application.Current.Resources.MergedDictionaries.Remove(resourceDictionary);
			}
			Theme.Refresh();
			// 切换皮肤后重新应用用户自定义颜色覆盖（旧字典随主题字典移除后需重建）
			App.ApplyCustomColors();
			NotificationCenter.Current.RaiseApplicationThemeChanged(this, newTheme);
		}
	}
}
