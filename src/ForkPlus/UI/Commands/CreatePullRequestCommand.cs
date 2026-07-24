using System;
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
	public class CreatePullRequestCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(string pullRequestUrl)
		{
			new Uri(pullRequestUrl).OpenInBrowser();
		}

		public void Execute(RepositoryUserControl repositoryUserControl, LocalBranch localBranch, RemoteBranch remoteBranch, string remote, string pullRequestUrl)
		{
			bool pushAllTags = ForkPlusSettings.Default.Push_PushAllTags;
			bool track = remoteBranch == null;
			bool force = false;
			repositoryUserControl.JobQueue.Add(ServiceLocator.Localization.FormatCurrent("Push '{0}' to '{1}'", localBranch.Name, remote), delegate(JobMonitor monitor)
			{
				GitCommandResult pushResult = new PushGitCommand().Execute(repositoryUserControl.GitModule, remote, localBranch, null, null, pushAllTags, force, track, monitor);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					if (!pushResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, pushResult.Error).ShowDialog();
					}
					else
					{
						repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References, new RevisionSelector.Head());
						new Uri(pullRequestUrl).OpenInBrowser();
					}
				});
			}, JobFlags.SaveToLog);
		}
	}
}
