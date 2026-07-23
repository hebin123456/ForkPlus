using System.Windows.Input;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class GitLfsLockCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Git LFS: Lock", new Argument[1]
			{
				new Argument(ArgumentType.RepositoryFile)
			}, delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (arguments[0] is string[] filePaths)
				{
					RepositoryUserControl.Commands.GitLfsLockCommand.Execute(repositoryUserControl, filePaths);
				}
			})
		};

		public string Title { get; } = "Lock";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, string[] filePaths)
		{
			repositoryUserControl.JobQueue.Add(Translate("LFS Lock"), delegate(JobMonitor monitor)
			{
				GitCommandResult lockResult = new GitLfsLockGitCommand().Execute(repositoryUserControl.GitModule, filePaths, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!lockResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, lockResult.Error).ShowDialog();
					}
				});
			});
		}

		private static string Translate(string text)
		{
			return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
