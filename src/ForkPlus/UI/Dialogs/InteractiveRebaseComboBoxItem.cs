using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public class InteractiveRebaseComboBoxItem
	{
		public string Title { get; }

		public string DisplayTitle => PreferencesLocalization.Translate(Title, ForkPlusSettings.Default.UiLanguage);

		public string Description { get; }

		public string DisplayDescription => PreferencesLocalization.Translate(Description, ForkPlusSettings.Default.UiLanguage);

		public string Shortcut { get; }

		public InteractiveRebaseAction? Action { get; }

		public bool IsSelectable { get; }

		public InteractiveRebaseComboBoxItem(InteractiveRebaseAction? action, string title, string description, string shortcut, bool isSelectable = true)
		{
			Title = title;
			Description = description;
			Shortcut = shortcut;
			Action = action;
			IsSelectable = isSelectable;
		}

		public override string ToString()
		{
			return DisplayTitle;
		}
	}
}
