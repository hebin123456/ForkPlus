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
			// 阶段 6 修复 NRE：TitleTextBlock/DescriptionTextBlock 在 Loaded → AddDialogHeader 才创建，
			// 构造时为 null。改用 ConfigureTitleTextBlock/ConfigureDescriptionTextBlock 缓存为 pending，
			// 由基类 AddDialogHeader 在创建 TextBlock 后统一应用。
			ConfigureTitleTextBlock(textTrimming: TextTrimming.CharacterEllipsis, textWrapping: TextWrapping.Wrap, maxHeight: 80.0);
			ConfigureDescriptionTextBlock(textTrimming: TextTrimming.CharacterEllipsis, textWrapping: TextWrapping.Wrap, maxHeight: 80.0);
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
