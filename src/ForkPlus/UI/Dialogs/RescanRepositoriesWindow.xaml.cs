using System;
using System.Threading.Tasks;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.UserControls.Preferences;
using System.ComponentModel;

namespace ForkPlus.UI.Dialogs
{
	public partial class RescanRepositoriesWindow : ForkPlusDialogWindow
	{
		private bool _isCloseAllowed = true;

		public RescanRepositoriesWindow()
		{
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Rescan repositories in source folder");
			string text = RepositoryManager.Instance.SourceDirs.Map((string x) => "'" + x + "'").Joined(", ");
			base.DialogDescription = PreferencesLocalization.FormatCurrent("Do you want to rescan repositories in '{0}'?", text);
			base.SubmitButtonTitle = PreferencesLocalization.Current("Rescan");
			ResetDeletedCheckbox.IsChecked = false;
		}

		protected override async void OnSubmit()
		{
			try
			{
				_isCloseAllowed = false;
				bool reset = ResetDeletedCheckbox.IsChecked.GetValueOrDefault();
				DisableEditableControls();
				SetStatus(ForkPlusDialogStatus.InProgress, "Scanning repositories...");
				await Task.Run(delegate
				{
					new RescanUserRepositoriesCommand().Execute(reset);
				});
				if (reset)
				{
					ForkPlusSettings.Default.RepositoryManagerTreeViewExpandedItems = null;
				}
				_isCloseAllowed = true;
				SetStatus(ForkPlusDialogStatus.Success, "Done");
				await Task.Delay(1500);
				CloseWithOk();
			}
			catch (Exception ex)
			{
				Log.Error("OnSubmit failed", ex);
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!_isCloseAllowed)
			{
				e.Cancel = true;
			}
			else
			{
				base.OnClosing(e);
			}
		}

	}
}
