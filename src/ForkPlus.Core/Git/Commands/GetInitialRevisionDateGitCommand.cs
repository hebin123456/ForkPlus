using System;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetInitialRevisionDateGitCommand
	{
		public DateTime? Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("log", "--no-show-signature", "--max-parents=0", "HEAD", "--pretty=format:%at", "--").Execute(silent: true);
			if (!gitRequestResult.Success)
			{
				return null;
			}
			if (DateTimeHelper.TryParseUnixDate(gitRequestResult.Stdout.Trim(), out var result))
			{
				return result;
			}
			return null;
		}
	}
}
