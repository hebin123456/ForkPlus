using System.Collections.Generic;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Services;

namespace ForkPlus.Git.Commands
{
	public class UnstageForAmendGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, ChangedFile[] files, JobMonitor monitor)
		{
			if (files.Length == 0)
			{
				return GitCommandResult.Success();
			}
			List<string> list = new List<string>();
			foreach (ChangedFile changedFile in files)
			{
				if (changedFile.Staged && !changedFile.IsDirectory && changedFile.TreeIsh != null && changedFile.FileMode != null)
				{
					if (changedFile.ChangeType == ChangeType.Renamed)
					{
						list.Add($"0 0000000000000000000000000000000000000000\t{changedFile.Path}\0");
						list.Add($"100644 {changedFile.TreeIsh}\t{changedFile.OldPath}\0");
					}
					else if (changedFile.New)
					{
						list.Add($"0 0000000000000000000000000000000000000000\t{changedFile.Path}\0");
					}
					else if (changedFile.ChangeType == ChangeType.Deleted)
					{
						list.Add($"100644 {changedFile.TreeIsh}\t{changedFile.Path}\0");
					}
					else
					{
						list.Add($"{changedFile.FileMode} {changedFile.TreeIsh}\t{changedFile.Path}\0");
					}
				}
			}
			if (list.Count > 0)
			{
				string s = string.Concat(list);
				byte[] bytes = Encoding.UTF8.GetBytes(s);
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("update-index", "-z", "--index-info").Stdin(bytes).Execute(3);
				if (!gitRequestResult.Success)
				{
					if (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr))
					{
						return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(gitRequestResult));
					}
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
			}
			monitor.Success(ServiceLocator.Localization.Current("unstaged"));
			return GitCommandResult.Success();
		}
	}
}
