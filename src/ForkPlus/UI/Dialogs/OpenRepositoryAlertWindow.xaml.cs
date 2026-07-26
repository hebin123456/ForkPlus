using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class OpenRepositoryAlertWindow : ForkPlusDialogWindow
	{

		public OpenRepositoryAlertWindow(string repositoryDirectory)
		{
			InitializeComponent();
			// 阶段 6 修复 NRE：DescriptionTextBlock 在 Loaded → AddDialogHeader 才创建，构造时为 null。
			ConfigureDescriptionTextBlock(textTrimming: TextTrimming.CharacterEllipsis, maxHeight: 80.0);
			base.DialogTitle = PreferencesLocalization.Current("The directory is not under git source control");
			base.DialogDescription = PreferencesLocalization.FormatCurrent("The '{0}' directory is not a git repository", repositoryDirectory);
			base.ShowSubmitButton = false;
			FirstButton.Content = PreferencesLocalization.Current("Initialize git repository here");
			base.CancelButtonTitle = PreferencesLocalization.Current("Close");
			base.Footer.SubmitButton.IsDefault = false;
			base.Footer.CancelButton.IsDefault = true;
			Dispatcher.UIThread.Async(delegate
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
