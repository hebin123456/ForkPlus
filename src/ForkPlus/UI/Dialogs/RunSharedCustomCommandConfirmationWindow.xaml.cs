using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class RunSharedCustomCommandConfirmationWindow : ForkPlusDialogWindow
	{

		public bool TrustThisRepository => TrustThisRepositoryCheckBox.IsChecked.GetValueOrDefault();

		public RunSharedCustomCommandConfirmationWindow(string repositoryName)
		{
			InitializeComponent();
			base.TitleTextBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			base.TitleTextBlock.TextWrapping = TextWrapping.Wrap;
			base.TitleTextBlock.MaxHeight = 80.0;
			base.DescriptionTextBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			base.DescriptionTextBlock.TextWrapping = TextWrapping.Wrap;
			base.DescriptionTextBlock.MaxHeight = 80.0;
			base.DialogTitle = "The custom command has come from the '" + repositoryName + "' repository";
			base.DialogDescription = "You should only run custom commands from trustworthy repositories. Do you really want to run it?";
			base.SubmitButtonTitle = "Run";
			base.CancelButtonTitle = "Cancel";
			base.ShowCancelButton = true;
			base.Width = 600.0;
			base.ShowWarningIcon = true;
			TrustThisRepositoryCheckBox.Content = PreferencesLocalization.FormatCurrent("Trust custom commands in '{0}'", repositoryName);
		}

	}
}
