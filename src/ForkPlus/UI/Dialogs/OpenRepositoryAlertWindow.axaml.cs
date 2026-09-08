using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	public partial class OpenRepositoryAlertWindow : ForkPlusDialogWindow
	{

		public OpenRepositoryAlertWindow(string repositoryDirectory)
		{
			InitializeComponent();
			base.DescriptionTextBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			base.DescriptionTextBlock.MaxHeight = 80.0;
			base.DialogTitle = PreferencesLocalization.Current("The directory is not under git source control");
			base.DialogDescription = PreferencesLocalization.FormatCurrent("The '{0}' directory is not a git repository", repositoryDirectory);
			base.ShowSubmitButton = false;
			FirstButton.Content = PreferencesLocalization.Current("Initialize git repository here");
			base.CancelButtonTitle = PreferencesLocalization.Current("Close");
			base.Footer.SubmitButton.IsDefault = false;
			base.Footer.CancelButton.IsDefault = true;
			base.Dispatcher.Post(delegate
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
