using ForkPlus.Services;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal sealed class RemoveTagGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, Tag[] tags, Remote[] remotes, JobMonitor monitor)
		{
			GitCommandResult gitCommandResult = DeleteRemoteTags(gitModule, tags.Map((Tag x) => x.FullReference), remotes, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult;
			}
			GitCommand gitCommand = new GitCommand("tag", "--delete");
			foreach (Tag tag in tags)
			{
				gitCommand.Add(tag.Name);
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		private GitCommandResult DeleteRemoteTags(GitModule gitModule, string[] tagFullReferences, Remote[] remotes, JobMonitor monitor)
		{
			foreach (Remote remote in remotes)
			{
				GitCommand gitCommand = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "push", remote.Name, "--delete");
				gitCommand.AddRange(tagFullReferences);
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
				if (!gitRequestResult.Success)
				{
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
			}
			return GitCommandResult.Success();
		}
	}
}
