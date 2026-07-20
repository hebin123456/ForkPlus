using System.Collections.Generic;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class UntrackFilesGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string[] filepaths, JobMonitor monitor)
		{
			GitCommand[] array = CreateUntrackGitCommands(filepaths);
			foreach (GitCommand command in array)
			{
				GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute(monitor, silent: true);
				if (!gitRequestResult.Success)
				{
					return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
				}
			}
			return GitCommandResult.Success();
		}

		private static GitCommand[] CreateUntrackGitCommands(string[] filePaths)
		{
			List<GitCommand> list = new List<GitCommand>();
			GitCommand gitCommand = new GitCommand();
			for (int i = 0; i < filePaths.Length; i++)
			{
				string argument = filePaths[i].Quotify();
				if (!gitCommand.CheckLimit(argument))
				{
					list.Add(gitCommand);
					gitCommand = new GitCommand();
				}
				if (gitCommand.IsEmpty)
				{
					gitCommand.Add("rm");
					gitCommand.Add("--cached");
					gitCommand.Add("--");
				}
				gitCommand.Add(argument);
			}
			if (!gitCommand.IsEmpty)
			{
				list.Add(gitCommand);
			}
			return list.ToArray();
		}
	}
}
