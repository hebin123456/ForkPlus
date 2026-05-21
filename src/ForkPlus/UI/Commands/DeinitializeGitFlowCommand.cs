using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class DeinitializeGitFlowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Deinitialize Git Flow";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Deinitialize Git Flow"), delegate(JobMonitor monitor)
			{
				GitCommandResult deinitializeGitFlowResult = new DeinitializeGitFlowGitCommand().Execute(gitModule, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!deinitializeGitFlowResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, deinitializeGitFlowResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.GitFlowSettings | SubDomain.References);
				});
			}, JobFlags.SaveToLog);
		}
	}
}
