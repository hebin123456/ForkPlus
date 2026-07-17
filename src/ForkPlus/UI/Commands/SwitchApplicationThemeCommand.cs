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
			NotificationCenter.Current.RaiseApplicationThemeChanged(this, newTheme);
		}
	}
}
