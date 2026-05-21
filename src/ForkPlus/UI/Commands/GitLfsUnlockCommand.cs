using System.Windows.Input;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class GitLfsUnlockCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git LFS: Unlock", new Argument[1]
			{
				new Argument(ArgumentType.RepositoryFile)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (arguments[0] is string[] filePaths)
				{
					RepositoryUserControl.Commands.GitLfsUnlockCommand.Execute(repositoryUserControl, filePaths);
				}
			})
		};

		public string Title { get; } = "Unlock";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, string[] filePaths)
		{
			repositoryUserControl.JobQueue.Add(Translate("LFS Unlock"), delegate(JobMonitor monitor)
			{
				GitCommandResult unlockResult = new GitLfsUnlockGitCommand().Execute(repositoryUserControl.GitModule, filePaths, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!unlockResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, unlockResult.Error).ShowDialog();
					}
				});
			});
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
