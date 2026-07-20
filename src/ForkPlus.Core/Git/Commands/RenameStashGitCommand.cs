using System;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	internal class RenameStashGitCommand
	{
		private static readonly string[] Separator = new string[1] { "±." };

		public GitCommandResult<Sha> Execute(GitModule gitModule, string stashReflogName, string newStashMessage, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("show", "--no-patch", "--pretty=format:%H±.%T±.%P", stashReflogName).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<Sha>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length != 3)
			{
				Log.Error("Cannot parse stash details line: '" + gitRequestResult.Stdout + "'");
				return GitCommandResult<Sha>.Failure(gitRequestResult.ToGitCommandError());
			}
			if (!Sha.TryParse(array[0], out var result))
			{
				return GitCommandResult<Sha>.Failure(new GitCommandError.ParseError("Failed to parse old stash SHA in '" + array[0] + "'"));
			}
			string tree = array[1];
			string[] parents = array[2].Split(Consts.Chars.Space, StringSplitOptions.RemoveEmptyEntries);
			GitCommandResult<Sha> gitCommandResult = CreateStashCommit(gitModule, tree, parents, newStashMessage, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return GitCommandResult<Sha>.Failure(gitCommandResult.Error);
			}
			Sha result2 = gitCommandResult.Result;
			GitCommandResult gitCommandResult2 = CreateStashObject(gitModule, result2, newStashMessage, monitor);
			if (!gitCommandResult2.Succeeded)
			{
				return GitCommandResult<Sha>.Failure(gitCommandResult2.Error);
			}
			GitCommandResult<string> stashReflogName2 = GetStashReflogName(gitModule, result);
			if (!stashReflogName2.Succeeded)
			{
				return GitCommandResult<Sha>.Failure(stashReflogName2.Error);
			}
			GitCommandResult gitCommandResult3 = DeleteOldStashObject(gitModule, stashReflogName2.Result, monitor);
			if (!gitCommandResult3.Succeeded)
			{
				return GitCommandResult<Sha>.Failure(gitCommandResult3.Error);
			}
			return GitCommandResult<Sha>.Success(result2);
		}

		private static GitCommandResult<Sha> CreateStashCommit(GitModule gitModule, string tree, string[] parents, string message, JobMonitor monitor)
		{
			GitCommand gitCommand = new GitCommand("commit-tree", tree);
			foreach (string argument in parents)
			{
				gitCommand.Add("-p");
				gitCommand.Add(argument);
			}
			gitCommand.Add("-m");
			gitCommand.Add(message.Quotify());
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<Sha>.Failure(gitRequestResult.ToGitCommandError());
			}
			string text = gitRequestResult.Stdout.Trim();
			if (!Sha.TryParse(text, out var result))
			{
				return GitCommandResult<Sha>.Failure(new GitCommandError.ParseError("Failed to parse stash SHA in '" + text + "'"));
			}
			return GitCommandResult<Sha>.Success(result);
		}

		private static GitCommandResult CreateStashObject(GitModule gitModule, Sha sha, string message, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("stash", "store", "--message", message.Quotify(), sha.ToString()).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}

		private static GitCommandResult<string> GetStashReflogName(GitModule gitModule, Sha sha)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("reflog", "--format=%H±.%gD", "stash").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			string text = sha.ToString();
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string[] array3 = array2[i].Split(Separator, StringSplitOptions.RemoveEmptyEntries);
				if (array3.Length != 2)
				{
					return GitCommandResult<string>.Failure(new GitCommandError.ParseError("Failed to parse reflog line: '" + gitRequestResult.Stdout));
				}
				if (array3[0] == text)
				{
					return GitCommandResult<string>.Success(array3[1]);
				}
			}
			return GitCommandResult<string>.Failure(new GitCommandError.NotFound());
		}

		private static GitCommandResult DeleteOldStashObject(GitModule gitModule, string stashReflogName, JobMonitor monitor)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("stash", "drop", stashReflogName).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
