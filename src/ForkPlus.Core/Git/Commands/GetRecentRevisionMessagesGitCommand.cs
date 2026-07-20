using System;
using System.Linq;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetRecentRevisionMessagesGitCommand
	{
		private static readonly int NumberOfRecentMessages = 10;

		public GitCommandResult<string[]> Execute(GitModule gitModule, LocalBranch activeBranch)
		{
			GitCommand gitCommand = new GitCommand();
			gitCommand.Add("log");
			gitCommand.Add("--no-show-signature");
			gitCommand.Add("-n");
			gitCommand.Add($"{NumberOfRecentMessages * 2}");
			gitCommand.Add("-z");
			gitCommand.Add("--pretty=%B");
			gitCommand.Add("HEAD");
			if (activeBranch != null)
			{
				gitCommand.Add(activeBranch.FullReference);
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteBt();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<string[]>.Success((from x in gitRequestResult.Stdout.Split(Consts.Chars.Nul, StringSplitOptions.RemoveEmptyEntries).Distinct().Take(NumberOfRecentMessages)
				select x.TrimEnd()).ToArray());
		}
	}
}
