using System.Windows.Controls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	public class HeaderMenuItem : MenuItem
	{
		public HeaderMenuItem(string title)
		{
			base.Header = PreferencesLocalization.MenuHeader(title);
			base.IsEnabled = false;
		}
	}
}
