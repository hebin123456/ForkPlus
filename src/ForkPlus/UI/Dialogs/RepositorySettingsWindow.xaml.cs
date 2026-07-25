using Avalonia.Controls;
using System;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.UserControls.RepositorySettings;
using System.ComponentModel;

namespace ForkPlus.UI.Dialogs
{
	public partial class RepositorySettingsWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly RepositoryData _repositoryData;

		public RepositorySettingsWindow(GitModule gitModule, RepositoryData repositoryData)
		{
			_gitModule = gitModule;
			_repositoryData = repositoryData;
			base.ShowLogo = false;
			InitializeComponent();
			base.ShowCancelButton = false;
			base.SubmitButtonTitle = PreferencesLocalization.Current("Close");
			base.SizeToContent = SizeToContent.WidthAndHeight;
			Initialize();
		}

		public void Initialize()
		{
			GeneralUserControl.Initialize(_gitModule);
			IssueTrackerUserControl.Initialize(this, _gitModule);
			CommitTemplateUserControl.Initialize(_gitModule);
			CustomCommandsUserControl.InitializeLocal(this, _gitModule, _repositoryData);
		}

		protected override void OnSubmit()
		{
			base.OnSubmit();
			GeneralUserControl.Save();
			IssueTrackerUserControl.Save();
			CommitTemplateUserControl.Save();
			CustomCommandsUserControl.Save();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			GeneralUserControl.Save();
			IssueTrackerUserControl.Save();
			CommitTemplateUserControl.Save();
			CustomCommandsUserControl.Save();
		}

	}
}
