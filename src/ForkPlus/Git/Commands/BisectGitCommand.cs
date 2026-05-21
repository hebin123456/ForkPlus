using System;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class BisectGitCommand
	{
		public enum BisectCommand
		{
			Start,
			Skip,
			Reset,
			Good,
			Bad
		}

		public GitCommandResult Execute(GitModule gitModule, BisectCommand bisectCommand, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("bisect", GetBisectCommandName(bisectCommand)).ExecuteBt(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			if (gitRequestResult.Stdout.Contains("is the first bad commit"))
			{
				return GitCommandResult.Failure(new GitCommandError.GitError(gitRequestResult.Stdout));
			}
			return GitCommandResult.Success();
		}

		private static string GetBisectCommandName(BisectCommand bisectCommand)
		{
			return bisectCommand switch
			{
				BisectCommand.Start => "start", 
				BisectCommand.Skip => "skip", 
				BisectCommand.Reset => "reset", 
				BisectCommand.Bad => "bad", 
				BisectCommand.Good => "good", 
				_ => throw new Exception(), 
			};
		}
	}
}
