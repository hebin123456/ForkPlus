using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Shell.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 执行 <c>git-mm.exe --version</c> 并返回原始版本字符串。
	/// </summary>
	public class GetGitMmVersionShellCommand
	{
		public GitCommandResult<string> Execute(string path)
		{
			try
			{
				if (!File.Exists(path))
				{
					Log.Error("Cannot find git-mm instance at: '" + path + "'");
					return GitCommandResult<string>.Failure(new GitCommandError.NotFound());
				}
				GitRequestResult gitRequestResult = new ShellRequest("", path, new string[1] { "--version" }).Execute();
				if (!gitRequestResult.Success)
				{
					return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
				}
				return GitCommandResult<string>.Success(gitRequestResult.Stdout.Trim());
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get git-mm version for '" + path + "'", ex);
				return GitCommandResult<string>.Failure(ex);
			}
		}
	}
}
