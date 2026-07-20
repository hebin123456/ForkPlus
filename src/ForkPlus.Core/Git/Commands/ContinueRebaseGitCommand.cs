using ForkPlus.Services;
using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class ContinueRebaseGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("diff-index", "HEAD", "--").Execute();
			string input = PathHelper.NormalizeUnix(Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.RIHelperFilename));
			if (gitRequestResult.Success && gitRequestResult.Stdout == "")
			{
				GitCommand command = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "-c", "core.commentChar=" + Consts.Git.CommentChar, "-c", "sequence.editor=" + input.EscapeSpaces().Quotify(), "-c", "core.editor=" + input.EscapeSpaces().Quotify(), "rebase", "--skip");
				GitRequestResult gitRequestResult2 = new GitRequest(gitModule).Command(command).Execute();
				if (!gitRequestResult2.Success)
				{
					return GitCommandResult.Failure(gitRequestResult2.ToGitCommandError());
				}
				return GitCommandResult.Success();
			}
			GitCommand command2 = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "-c", "core.commentChar=" + Consts.Git.CommentChar, "-c", "sequence.editor=" + input.EscapeSpaces().Quotify(), "-c", "core.editor=" + input.EscapeSpaces().Quotify(), "rebase", "--continue");
			GitRequestResult gitRequestResult3 = new GitRequest(gitModule).Command(command2).Execute();
			if (!gitRequestResult3.Success)
			{
				return GitCommandResult.Failure(gitRequestResult3.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
