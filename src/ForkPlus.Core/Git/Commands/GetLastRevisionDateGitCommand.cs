using System;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetLastRevisionDateGitCommand
	{
		public DateTime? Execute(GitModule gitModule)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("log", "--no-show-signature", "-n", "1", "--pretty=format:%at", "--").Execute();
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
