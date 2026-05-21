using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class OpenRepositoryAlertWindow : ForkPlusDialogWindow
	{

		public OpenRepositoryAlertWindow(string repositoryDirectory)
		{
			InitializeComponent();
			base.DescriptionTextBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			base.DescriptionTextBlock.MaxHeight = 80.0;
			base.DialogTitle = "The directory is not under git source control";
			base.DialogDescription = "The '" + repositoryDirectory + "' directory is not a git repository";
			base.ShowSubmitButton = false;
			FirstButton.Content = PreferencesLocalization.Current("Initialize git repository here");
			base.CancelButtonTitle = "Close";
			base.Footer.SubmitButton.IsDefault = false;
			base.Footer.CancelButton.IsDefault = true;
			base.Dispatcher.Async(delegate
			{
				base.Footer.CancelButton.Focus();
			});
		}

		private void FirstButton_Click(object sender, RoutedEventArgs e)
		{
			CloseWithOk();
		}

	}
}
