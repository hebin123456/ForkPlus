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
	public class DeinitializeGitLfsCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Deinitialize Git LFS";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			repositoryUserControl.JobQueue.Add(Translate("Deinitialize Git LFS"), delegate(JobMonitor monitor)
			{
				GitCommandResult deinitializeGitLfsResult = new GitLfsUninstallGitCommand().Execute(gitModule, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!deinitializeGitLfsResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, deinitializeGitLfsResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.RepositoryData);
				});
			}, JobFlags.SaveToLog);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
