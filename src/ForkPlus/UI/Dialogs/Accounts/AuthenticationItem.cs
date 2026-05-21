using System.ComponentModel;
using ForkPlus.Utils.Http;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs.Accounts
{
	public class AuthenticationItem : INotifyPropertyChanged
	{
		public AuthenticationType Type { get; }

		public string Title { get; }

		public event PropertyChangedEventHandler PropertyChanged;

		public AuthenticationItem(AuthenticationType type, string title)
		{
			Type = type;
			Title = PreferencesLocalization.Translate(title, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
