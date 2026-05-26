using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.Git.Commands
{
	public class FastForwardPullGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, RemoteBranch remoteBranch, LocalBranch localBranch, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper);
			if (localBranch.IsActive)
			{
				gitCommand.AddRange("pull", "--ff-only", remoteBranch.Remote, remoteBranch.ShortName, "--progress");
			}
			else
			{
				gitCommand.AddRange("fetch", remoteBranch.Remote, remoteBranch.ShortName + ":" + localBranch.Name, "--progress");
			}
			monitor.Update(0.0, PreferencesLocalization.Current("Pulling..."));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteLong(delegate(string stdOutLine)
			{
				monitor.AppendOutputLine(stdOutLine);
			}, delegate(string stdErrLine)
			{
				Log.Debug(stdErrLine);
				if (!monitor.HandleGitProgress(stdErrLine))
				{
					monitor.AppendOutputLine(stdErrLine);
				}
			}, monitor);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (gitRequestResult.Success)
			{
				monitor.Success(PreferencesLocalization.Current("Everything is up to date"));
			}
			else
			{
				string stderr = gitRequestResult.Stderr;
				if (stderr != null)
				{
					monitor.Fail(stderr);
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
			}
			return GitCommandResult.Success();
		}
	}
}
