using Avalonia.Media;
using System;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class MessageBoxWindow : ForkPlusDialogWindow
	{

		public MessageBoxWindow(string title, string description, string submitTitle, string cancelTitle = "Cancel", bool showCancelButton = true, double width = 600.0, bool showWarningIcon = false)
		{
			InitializeComponent();
			base.TitleTextBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			base.TitleTextBlock.TextWrapping = TextWrapping.Wrap;
			base.TitleTextBlock.MaxHeight = 80.0;
			base.DescriptionTextBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			base.DescriptionTextBlock.TextWrapping = TextWrapping.Wrap;
			base.DescriptionTextBlock.MaxHeight = 80.0;
			base.DialogTitle = Translate(title);
			base.DialogDescription = Translate(description);
			base.SubmitButtonTitle = Translate(submitTitle);
			base.CancelButtonTitle = Translate(cancelTitle);
			base.ShowCancelButton = showCancelButton;
			base.Width = width;
			base.ShowWarningIcon = showWarningIcon;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
