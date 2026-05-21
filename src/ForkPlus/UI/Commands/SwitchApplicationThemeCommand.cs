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

		public void Execute()
		{
			ThemeType themeType = ((ForkPlusSettings.Default.Theme != ThemeType.Dark) ? ThemeType.Dark : ThemeType.Light);
			ForkPlusSettings.Default.Theme = themeType;
			Execute(themeType);
		}

		public void Execute(ThemeType newTheme, bool followSystemTheme = false)
		{
			ForkPlusSettings.Default.Theme = newTheme;
			ForkPlusSettings.Default.FollowSystemTheme = followSystemTheme;
			App.RefreshWindowBorderBrush();
			ResourceDictionary resourceDictionary = Application.Current.Resources.MergedDictionaries.Where((ResourceDictionary rd) => rd.Source != null).FirstOrDefault((ResourceDictionary rd) => Regex.Match(rd.Source.OriginalString, "(\\/ForkPlus;component\\/Theme\\/Generic\\.)((Light)|(Dark))").Success);
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

