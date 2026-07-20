using ForkPlus.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetLfsLocksGitCommand
	{
		public class LfsLock
		{
			public string Path { get; }

			public string Owner { get; }

			public LfsLock(string path, string owner)
			{
				Path = path;
				Owner = owner;
			}
		}

		public GitCommandResult<Dictionary<string, string>> Execute(GitModule gitModule, JobMonitor monitor)
		{
			GitCommand command = new GitCommand(ServiceLocator.GitEnvironment.OverrideCredentialHelper, "lfs", "locks");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(command).Execute();
			if (!gitRequestResult.Success)
			{
				if (monitor.IsCanceled)
				{
					return GitCommandResult<Dictionary<string, string>>.Failure(new GitCommandError.Cancelled());
				}
				return GitCommandResult<Dictionary<string, string>>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<Dictionary<string, string>>.Success(gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries).CompactMap((string x) => ParseLfsLock(x)).ToDictionary((LfsLock x) => x.Path, (LfsLock x) => x.Owner));
		}

		[Null]
		private static LfsLock ParseLfsLock(string line)
		{
			string[] array = line.Split(Consts.Chars.Tab);
			if (array.Length != 3)
			{
				Log.Warn("Can't parse lfs lock in '" + line + "'");
				return null;
			}
			return new LfsLock(array[0].TrimEnd(), array[1]);
		}
	}
}
