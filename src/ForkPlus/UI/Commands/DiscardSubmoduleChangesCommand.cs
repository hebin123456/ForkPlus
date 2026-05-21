using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class DiscardSubmoduleChangesCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Discard submodule changes...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, Submodule[] submodules)
		{
			if (submodules.Length != 0)
			{
				Submodule submodule = submodules.SingleItem();
				MessageBoxWindow messageBoxWindow = ((submodule == null) ? new MessageBoxWindow(string.Format(Translate("Do you want to discard changes in {0} submodules?"), submodules.Length), Translate("All uncommitted changes will be lost"), Translate("Discard All"), Translate("Cancel"), showCancelButton: true, 550.0) : new MessageBoxWindow(Translate("Do you want to discard changes in the submodule?"), string.Format(Translate("Discard all changes in '{0}'?"), submodule.Path), Translate("Discard"), Translate("Cancel"), showCancelButton: true, 550.0));
				if (messageBoxWindow.ShowDialog().GetValueOrDefault())
				{
					DiscardSubmodules(commitUserControl, repositoryUserControl, submodules);
				}
			}
		}

		private void DiscardSubmodules(CommitUserControl commitUserControl, RepositoryUserControl repositoryUserControl, Submodule[] submodules)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null || commitUserControl.StageJob != null)
			{
				return;
			}
			commitUserControl.StageJob = repositoryUserControl.JobQueue.Add(Translate("Discard Submodules"), delegate(JobMonitor monitor)
			{
				GitCommandResult discardResult = null;
				Submodule[] array = submodules;
				foreach (Submodule submodule in array)
				{
					discardResult = new DiscardAllSubmoduleChangesGitCommand().Execute(gitModule, submodule);
					if (!discardResult.Succeeded)
					{
						commitUserControl.Dispatcher.Async(delegate
						{
							new ErrorWindow(repositoryUserControl, discardResult.Error).ShowDialog();
						});
						break;
					}
				}
				commitUserControl.Dispatcher.Async(delegate
				{
					if (!monitor.IsCanceled)
					{
						commitUserControl.StageJob = null;
						commitUserControl.RefreshStageControls();
						commitUserControl.UpdateCommitSection(updateWarningMessage: false);
						string[] array2 = submodules.Map((Submodule x) => x.Path);
						GitCommandResult gitCommandResult = discardResult;
						if ((gitCommandResult != null && !gitCommandResult.Succeeded) || ExceedLength(array2))
						{
							SubDomain subdomains = SubDomain.Status;
							repositoryUserControl.InvalidateAndRefresh(subdomains, null, RepositoryViewMode.CommitViewMode);
						}
						else
						{
							new RefreshFileStatusCommand().Execute(commitUserControl, repositoryUserControl, array2);
						}
					}
				});
			}, JobFlags.SaveToLog | JobFlags.ShowOnToolbar);
			commitUserControl.RefreshStageControls();
			commitUserControl.UpdateCommitSection(updateWarningMessage: false);
		}

		private static bool ExceedLength(string[] paths)
		{
			int num = 0;
			foreach (string text in paths)
			{
				num += text.Length + 1;
				if (num > Consts.Env.ArgumentLengthLimit)
				{
					return true;
				}
			}
			return false;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
