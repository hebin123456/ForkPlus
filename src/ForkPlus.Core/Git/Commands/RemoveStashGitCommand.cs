using System.Linq;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class RemoveStashGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, StashRevision[] stashes, JobMonitor monitor)
		{
			StashRevision[] array = stashes.OrderByDescending((StashRevision x) => x.ReflogName, NaturalStringComparer.Instance).ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				string reflogName = array[i].ReflogName;
				string text = (reflogName.StartsWith("stash") ? ("refs/" + reflogName) : reflogName);
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("stash", "drop", text).Execute(monitor);
				if (!gitRequestResult.Success)
				{
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
			}
			return GitCommandResult.Success();
		}
	}
}
