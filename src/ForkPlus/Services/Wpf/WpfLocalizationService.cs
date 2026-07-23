using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Services.Wpf
{
	/// <summary>
	/// WPF 平台的 <see cref="ILocalizationService"/> 实现，委托给
	/// <see cref="PreferencesLocalization"/> 静态类（加载 Languages/*.json 翻译字典）。
	/// </summary>
	public class WpfLocalizationService : ILocalizationService
	{
		public string Current(string text)
		{
			return PreferencesLocalization.Current(text);
		}

		public string Translate(string text, string language)
		{
			return PreferencesLocalization.Translate(text, language);
		}

		public string FormatCurrent(string text, params object[] args)
		{
			return PreferencesLocalization.FormatCurrent(text, args);
		}
	}
}
