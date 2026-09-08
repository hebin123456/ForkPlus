using System;
using ForkPlus;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class UpdateAvailableWindow : ForkPlusDialogWindow
	{
		private readonly UpdateInfo _updateInfo;

		public UpdateAvailableWindow(UpdateInfo updateInfo)
		{
			InitializeComponent();
			_updateInfo = updateInfo;
			DialogTitle = PreferencesLocalization.Current("Update Available");
			DialogDescription = PreferencesLocalization.FormatCurrent(
				"A new version {0} is available (current: {1}).",
				updateInfo.LatestVersion, updateInfo.CurrentVersion);
			SubmitButtonTitle = PreferencesLocalization.Current("Download");
			CancelButtonTitle = PreferencesLocalization.Current("Later");
			ReleaseNotesTextBox.Text = string.IsNullOrEmpty(updateInfo.ReleaseNotes)
				? updateInfo.ReleaseName
				: updateInfo.ReleaseNotes;
		}

		protected override void OnSubmit()
		{
			try
			{
				if (!string.IsNullOrEmpty(_updateInfo.DownloadUrl))
				{
					new Uri(_updateInfo.DownloadUrl).OpenInBrowser();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to open download url", ex);
			}
			if (SkipVersionCheckBox.IsChecked == true)
			{
				UpdateChecker.SkipVersion(_updateInfo.LatestVersion);
			}
			base.OnSubmit();
		}

		protected override void OnCancel()
		{
			if (SkipVersionCheckBox.IsChecked == true)
			{
				UpdateChecker.SkipVersion(_updateInfo.LatestVersion);
			}
			base.OnCancel();
		}
	}
}
