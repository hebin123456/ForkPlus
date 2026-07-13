using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class DeleteSubmoduleWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly Submodule _submodule;

		public DeleteSubmoduleWindow(GitModule gitModule, Submodule submodule)
		{
			_gitModule = gitModule;
			_submodule = submodule;
			InitializeComponent();
			base.DialogTitle = string.Format(Translate("Are you sure you want to delete submodule {0}?"), submodule.FriendlyName);
			base.DialogDescription = string.Format(Translate("Do you want to delete submodule {0}?"), submodule.Path);
			base.SubmitButtonTitle = Translate("Delete");
		}

		protected override string GetCommandPreview()
		{
			string path = _submodule.Path;
			string Quote(string s) => s.Contains(" ") ? "\"" + s + "\"" : s;
			return "git submodule deinit -f " + Quote(path) + " && git rm -f " + Quote(path);
		}

		protected override void OnSubmit()
		{
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting submodule..."));
			MainWindow.ActiveRepositoryUserControl.JobQueue.Add(string.Format(Translate("Delete submodule '{0}'"), _submodule.FriendlyName), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new DeleteSubmoduleGitCommand().Execute(_gitModule, _submodule.Path, monitor);
				base.Dispatcher.Async(delegate
				{
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
