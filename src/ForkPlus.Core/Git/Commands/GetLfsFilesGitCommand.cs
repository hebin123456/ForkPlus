using ForkPlus.Services;
using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetLfsFilesGitCommand
	{
		public GitCommandResult<string[]> Execute(GitModule gitModule, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "lfs", "ls-files");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute();
			if (!gitRequestResult.Success)
			{
				if (monitor.IsCanceled)
				{
					return GitCommandResult<string[]>.Failure(new GitCommandError.Cancelled());
				}
				return GitCommandResult<string[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			List<string> list = new List<string>(array.Length);
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string text = ParseLfsFile(array2[i]);
				if (text != null)
				{
					list.Add(text);
				}
			}
			list.Sort((string x, string y) => NaturalStringComparer.Instance.Compare(x, y));
			return GitCommandResult<string[]>.Success(list.ToArray());
		}

		[Null]
		private static string ParseLfsFile(string line)
		{
			int num = line.IndexOf(Consts.Chars.SpaceChar);
			if (num == -1)
			{
				return null;
			}
			int num2 = line.IndexOf(Consts.Chars.SpaceChar, num + 1);
			if (num2 == -1)
			{
				return null;
			}
			return line.Substring(num2 + 1);
		}
	}
}
