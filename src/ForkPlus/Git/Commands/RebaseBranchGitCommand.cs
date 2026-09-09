using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class RebaseBranchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string destination, bool rebaseMerges, bool updateRefs, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "-c", "core.commentChar=" + Consts.Git.CommentChar, "rebase");
			if (rebaseMerges)
			{
				gitCommand.Add("--rebase-merges");
			}
			if (updateRefs)
			{
				// v4.0.5：--update-refs 需 git ≥2.38，老版本带上整条命令报错。UI 侧已按
				// 能力隐藏开关，此处兜底防御（设置残留勾选状态等旁路进入）。
				if (GitCapabilities.SupportsUpdateRefs())
				{
					gitCommand.Add("--update-refs");
				}
				else
				{
					Log.Info("git < 2.38 does not support rebase --update-refs, option skipped");
				}
			}
			gitCommand.Add(destination);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
			if (GitCommandError.AutomaticMergeFailed.Match(gitRequestResult))
			{
				return GitCommandResult.Failure(new GitCommandError.AutomaticMergeFailed(gitRequestResult));
			}
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
