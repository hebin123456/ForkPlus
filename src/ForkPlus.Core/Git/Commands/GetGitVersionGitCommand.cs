using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Shell.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetGitVersionGitCommand
	{
		public GitCommandResult<string> Execute(string path)
		{
			try
			{
				if (!File.Exists(path))
				{
					Log.Error("Cannot find git instance at: '" + path + "'");
					return GitCommandResult<string>.Failure(new GitCommandError.NotFound());
				}
				GitRequestResult gitRequestResult = new ShellRequest("", path, new string[1] { "version" }).Execute();
				if (!gitRequestResult.Success)
				{
					return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
				}
				string[] array = gitRequestResult.Stdout.Split(new string[1] { "version" }, StringSplitOptions.RemoveEmptyEntries);
				if (array.Length != 2)
				{
					Log.Error("Cannot parse git instance version: " + gitRequestResult.Stdout);
					return GitCommandResult<string>.Failure(new GitCommandError.NotFound());
				}
				return GitCommandResult<string>.Success(array[1].Trim());
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get git instance for '" + path + "'", ex);
				return GitCommandResult<string>.Failure(ex);
			}
		}
	}
}
