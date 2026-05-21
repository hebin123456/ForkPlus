using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class DisableImplicitRemoteFetchGitCommand
	{
		private const string DisableImplicitFetchName = "disableimplicitfetch";

		public GitCommandResult Execute(GitModule gitModule, Remote remote, bool disableImplicitFetch)
		{
			if (disableImplicitFetch)
			{
				Log.Info("Disable automatic fetch for '" + remote.Name + "'");
			}
			else
			{
				Log.Info("Enable automatic fetch for '" + remote.Name + "'");
			}
			string text = (disableImplicitFetch ? "true" : "false");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("config", "remote." + remote.Name + ".disableimplicitfetch", text).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
