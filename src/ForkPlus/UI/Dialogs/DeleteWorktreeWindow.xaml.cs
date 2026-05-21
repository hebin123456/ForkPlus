using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class DeleteWorktreeWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly Worktree _worktree;

		public DeleteWorktreeWindow(RepositoryUserControl repositoryUserControl, Worktree worktree)
		{
			_repositoryUserControl = repositoryUserControl;
			_worktree = worktree;
			InitializeComponent();
			base.DialogTitle = "Are you sure you want to delete worktree " + worktree.FriendlyName + "?";
			base.DialogDescription = "Do you want to delete worktree " + worktree.Path + "?";
			base.SubmitButtonTitle = "Delete";
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			Worktree worktree = _worktree;
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, PreferencesLocalization.Current("Deleting worktree..."));
			_repositoryUserControl.JobQueue.Add(PreferencesLocalization.FormatCurrent("Delete worktree '{0}'", worktree.FriendlyName), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new RemoveWorktreeGitCommand().Execute(gitModule, worktree.Path, monitor);
				base.Dispatcher.Async(delegate
				{
					if (result.Succeeded)
					{
						MainWindow.Instance.TabManager.CloseTab(worktree.Path);
					}
					Close(result);
				});
			}, JobFlags.SaveToLog);
		}

	}
}
