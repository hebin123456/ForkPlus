using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class ResetFileToStateAtRevisionCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "State at Commit...";

		public KeyGesture Shortcut { get; }

		public KeyGesture SecondaryShortcut { get; }

		public void Execute(RepositoryUserControl repositoryUserControl, ChangedFile[] changedFiles, string shaString)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			string title = GetTitle(changedFiles, shaString);
			// v3.4.1：状态栏标题国际化（之前是硬编码英文）
			string text = ((changedFiles.Length > 1) ? ServiceLocator.Localization.Current("Reset Files") : ServiceLocator.Localization.Current("Reset File"));
			if (!new MessageBoxWindow(title, "The current file changes will be discarded.", text).ShowDialog().GetValueOrDefault())
			{
				return;
			}
			repositoryUserControl.JobQueue.Add(text, delegate(JobMonitor monitor)
			{
				GitCommandResult resetResult = new ResetFilesAtRevisionGitCommand().Execute(gitModule, changedFiles, shaString, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
					if (!resetResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, resetResult.Error).ShowDialog();
					}
				});
			});
		}

		private static string GetTitle(ChangedFile[] changedFiles, string shaString)
		{
			if (shaString.EndsWith("~"))
			{
				if (changedFiles.Length <= 1)
				{
					return "Do you want to reset the file to the state it was before the commit?";
				}
				return $"Do you want to reset {changedFiles.Length} files to the state they were before the commit?";
			}
			if (changedFiles.Length <= 1)
			{
				return "Do you want to reset the file to the state it is at the commit?";
			}
			return $"Do you want to reset {changedFiles.Length} files to the state they are at the commit?";
		}
	}
}
