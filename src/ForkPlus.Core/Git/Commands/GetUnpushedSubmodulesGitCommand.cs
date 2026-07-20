using System.Collections.Generic;
using System.IO;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetUnpushedSubmodulesGitCommand
	{
		public GitCommandResult<string[]> Execute(GitModule parentGitModule, Sha sha, string[] submodulePaths, JobMonitor monitor)
		{
			List<string> list = new List<string>(submodulePaths.Length);
			foreach (string text in submodulePaths)
			{
				GitCommandResult<Sha> gitCommandResult = new GetSubmoduleHeadGitCommand().Execute(parentGitModule, sha, text, monitor);
				if (!gitCommandResult.Succeeded)
				{
					return GitCommandResult<string[]>.Failure(gitCommandResult.Error);
				}
				GitCommandResult<GitModule> gitCommandResult2 = new OpenGitRepositoryGitCommand().Execute(parentGitModule.MakePath(text));
				if (!gitCommandResult2.Succeeded)
				{
					return GitCommandResult<string[]>.Failure(gitCommandResult2.Error);
				}
				GitCommandResult<string[]> gitCommandResult3 = new GetRemoteBranchesContainingShaGitCommand().Execute(gitCommandResult2.Result, gitCommandResult.Result, monitor);
				if (!gitCommandResult3.Succeeded)
				{
					return GitCommandResult<string[]>.Failure(gitCommandResult3.Error);
				}
				if (gitCommandResult3.Result.Length == 0)
				{
					list.Add(Path.GetFileName(text));
				}
			}
			return GitCommandResult<string[]>.Success(list.ToArray());
		}
	}
}
