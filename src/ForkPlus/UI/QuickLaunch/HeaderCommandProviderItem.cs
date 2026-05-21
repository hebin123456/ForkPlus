using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.QuickLaunch
{
	public class HeaderCommandProviderItem : CommandProviderItem
	{
		public HeaderCommandProviderItem(string name)
			: base(name, PreferencesLocalization.Translate(name, ForkPlusSettings.Default.UiLanguage), "")
		{
		}
	}
}
