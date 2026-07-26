using Avalonia.Media;
using System;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls.Documents;

namespace ForkPlus.UI.Dialogs
{
	public partial class RunSharedCustomCommandConfirmationWindow : ForkPlusDialogWindow
	{

		public bool TrustThisRepository => TrustThisRepositoryCheckBox.IsChecked.GetValueOrDefault();

		public RunSharedCustomCommandConfirmationWindow(string repositoryName)
		{
			InitializeComponent();
			// 阶段 6 修复 NRE：TitleTextBlock/DescriptionTextBlock 在 Loaded → AddDialogHeader 才创建，
			// 构造时为 null。改用 ConfigureTitleTextBlock/ConfigureDescriptionTextBlock 缓存为 pending。
			ConfigureTitleTextBlock(textTrimming: TextTrimming.CharacterEllipsis, textWrapping: TextWrapping.Wrap, maxHeight: 80.0);
			ConfigureDescriptionTextBlock(textTrimming: TextTrimming.CharacterEllipsis, textWrapping: TextWrapping.Wrap, maxHeight: 80.0);
			base.DialogTitle = PreferencesLocalization.FormatCurrent("The custom command has come from the '{0}' repository", repositoryName);
			base.DialogDescription = PreferencesLocalization.Current("You should only run custom commands from trustworthy repositories. Do you really want to run it?");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Run");
			base.CancelButtonTitle = PreferencesLocalization.Current("Cancel");
			base.ShowCancelButton = true;
			base.Width = 600.0;
			base.ShowWarningIcon = true;
			TrustThisRepositoryCheckBox.Content = PreferencesLocalization.FormatCurrent("Trust custom commands in '{0}'", repositoryName);
		}

	}
}
