using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class InitGitFlowGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, GitFlowSettings gitFlowSettings, JobMonitor monitor)
		{
			GitCommandResult<ChangedFilesCollection> gitCommandResult = new GetChangedFilesGitCommand().Execute(gitModule);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult.ToGitCommandResult();
			}
			if (gitCommandResult.Result.ChangedFiles.Length != 0)
			{
				return GitCommandResult.Failure(new GitCommandError.WorkingDirectoryIsDirty());
			}
			new GitRequest(gitModule).Command("branch", "--no-track", gitFlowSettings.MasterBranch).ExecuteBt(monitor, silent: true);
			new GitRequest(gitModule).Command("branch", "--no-track", gitFlowSettings.DevelopBranch).ExecuteBt(monitor, silent: true);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "gitflow.branch.develop", gitFlowSettings.DevelopBranch).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command("config", "gitflow.branch.master", gitFlowSettings.MasterBranch).ExecuteBt(monitor);
			if (!gitRequestResult2.Success)
			{
				return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
			}
			GitRequestResult gitRequestResult3 = new GitRequest(gitModule).Command("config", "gitflow.prefix.feature", gitFlowSettings.FeaturePrefix).ExecuteBt(monitor);
			if (!gitRequestResult3.Success)
			{
				return GitCommandResult.Failure(gitRequestResult3.ToGitCommandError());
			}
			GitRequestResult gitRequestResult4 = new GitRequest(gitModule).Command("config", "gitflow.prefix.release", gitFlowSettings.ReleasePrefix).ExecuteBt(monitor);
			if (!gitRequestResult4.Success)
			{
				return GitCommandResult.Failure(gitRequestResult4.ToGitCommandError());
			}
			GitRequestResult gitRequestResult5 = new GitRequest(gitModule).Command("config", "gitflow.prefix.hotfix", gitFlowSettings.HotfixPrefix).ExecuteBt(monitor);
			if (!gitRequestResult5.Success)
			{
				return GitCommandResult.Failure(gitRequestResult5.ToGitCommandError());
			}
			GitRequestResult gitRequestResult6 = new GitRequest(gitModule).Command("config", "gitflow.prefix.versiontag", gitFlowSettings.VersionTag).ExecuteBt(monitor);
			if (!gitRequestResult6.Success)
			{
				return GitCommandResult.Failure(gitRequestResult6.ToGitCommandError());
			}
			GitRequestResult gitRequestResult7 = new GitRequest(gitModule).Command("config", "gitflow.prefix.bugfix", "bugfix/").ExecuteBt(monitor);
			if (!gitRequestResult7.Success)
			{
				return GitCommandResult.Failure(gitRequestResult7.ToGitCommandError());
			}
			GitRequestResult gitRequestResult8 = new GitRequest(gitModule).Command("config", "gitflow.prefix.support", "support/").ExecuteBt(monitor);
			if (!gitRequestResult8.Success)
			{
				return GitCommandResult.Failure(gitRequestResult8.ToGitCommandError());
			}
			string text = gitModule.HooksDirectoryPath().Replace('\\', '/');
			GitRequestResult gitRequestResult9 = new GitRequest(gitModule).Command("config", "gitflow.path.hooks", text).ExecuteBt(monitor);
			if (!gitRequestResult9.Success)
			{
				return GitCommandResult.Failure(gitRequestResult9.ToGitCommandError());
			}
			GitRequestResult gitRequestResult10 = new GitRequest(gitModule).Command("flow", "init", "-d").ExecuteBt(monitor);
			if (!gitRequestResult10.Success)
			{
				return GitCommandResult.Failure(gitRequestResult10.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
