using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Settings;

namespace ForkPlus.UI.Controls
{
	public class SpellingPlaceholderTextBox : AutoCompleteTextBox
	{
		public SpellingPlaceholderTextBox()
		{
			base.ContextMenuOpening += delegate
			{
				base.ContextMenu = GetContextMenu();
				SpellingError spellingError = GetSpellingError(base.CaretIndex);
				base.ContextMenu.AddSpellingMenuItems(spellingError, this);
			};
			if (!global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				WeakEventManager<NotificationCenter, EventArgs<CommitSpellCheckingMode>>.AddHandler(NotificationCenter.Current, "CommitSpellCheckingModeChanged", delegate
				{
					RefreshSpellChecking();
				});
			}
			RefreshSpellChecking();
		}

		public void RefreshSpellChecking()
		{
			switch (ForkPlusSettings.Default.CommitSpellCheckingMode)
			{
			case CommitSpellCheckingMode.Disable:
				base.SpellCheck.IsEnabled = false;
				break;
			case CommitSpellCheckingMode.System:
				base.SpellCheck.IsEnabled = true;
				base.Language = XmlLanguage.GetLanguage(CultureInfo.InstalledUICulture.Name);
				break;
			case CommitSpellCheckingMode.English:
				base.SpellCheck.IsEnabled = true;
				base.Language = XmlLanguage.GetLanguage("en-US");
				break;
			}
		}
	}
}
