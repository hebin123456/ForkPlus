using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class InitGitLfsCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Initialize Git LFS";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			repositoryUserControl.JobQueue.Add(Translate("Initialize Git LFS"), delegate(JobMonitor monitor)
			{
				GitCommandResult initializeGitLfsResult = new GitLfsInstallGitCommand().Execute(gitModule, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!initializeGitLfsResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, initializeGitLfsResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.RepositoryData);
				});
			}, JobFlags.SaveToLog);
		}

		private static string Translate(string text)
		{
			return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
