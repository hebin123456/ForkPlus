using System;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.Services;

namespace ForkPlus.UI.Commands
{
	public class BisectCommand : IUICommand, IForkPlusCommand
	{
		public static CommandDescriptor[] PublicCommands = new CommandDescriptor[1]
		{
			new CommandDescriptor("Bisect", new Argument[0], delegate(object[] arguments, RepositoryUserControl repositoryUserControl)
			{
				if (repositoryUserControl.GitModule != null)
				{
					RepositoryUserControl.Commands.Bisect.Execute(repositoryUserControl, BisectGitCommand.BisectCommand.Start);
				}
			})
		};

		public string Title => "Bisect";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, BisectGitCommand.BisectCommand bisectCommand)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
			repositoryUserControl.JobQueue.Add(string.Format(Translate("Bisect: {0}"), Translate(GetBisectCommandName(bisectCommand))), delegate(JobMonitor monitor)
			{
				GitCommandResult bisectResult = PerformBisect(gitModule, bisectCommand, submodulesToUpdate, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!bisectResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, bisectResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Head | SubDomain.References, new RevisionSelector.Head());
				});
			});
		}

		private static GitCommandResult PerformBisect(GitModule gitModule, BisectGitCommand.BisectCommand bisectCommand, SubmodulesToUpdate submodulesToUpdate, JobMonitor monitor)
		{
			GitCommandResult gitCommandResult = new BisectGitCommand().Execute(gitModule, bisectCommand, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult;
			}
			if (submodulesToUpdate.Length > 0)
			{
				GitCommandResult gitCommandResult2 = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
				if (!gitCommandResult2.Succeeded)
				{
					return gitCommandResult2;
				}
			}
			return gitCommandResult;
		}

		private static string GetBisectCommandName(BisectGitCommand.BisectCommand bisectCommand)
		{
			return bisectCommand switch
			{
				BisectGitCommand.BisectCommand.Start => "start", 
				BisectGitCommand.BisectCommand.Skip => "skip", 
				BisectGitCommand.BisectCommand.Reset => "reset", 
				BisectGitCommand.BisectCommand.Bad => "bad", 
				BisectGitCommand.BisectCommand.Good => "good", 
				_ => throw new Exception(), 
			};
		}

		private static string Translate(string text)
		{
			return ServiceLocator.Localization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
