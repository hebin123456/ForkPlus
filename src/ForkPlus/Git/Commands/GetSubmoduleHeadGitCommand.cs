using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class GetSubmoduleHeadGitCommand
	{
		public GitCommandResult<Sha> Execute(GitModule gitModule, Sha sha, string submodulePath, JobMonitor monitor)
		{
			string text = PathHelper.NormalizeUnix(Path.GetDirectoryName(submodulePath));
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("ls-tree", "--format=%(objecttype)%x20%(objectname)%x09%(path)", sha.ToString() + ":" + text).Execute(monitor);
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<Sha>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			string fileName = Path.GetFileName(submodulePath);
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string[] array3 = array2[i].Split(Consts.Chars.Space);
				if (array3.Length != 2 || array3[0] != "commit")
				{
					continue;
				}
				string[] array4 = array3[1].Split(Consts.Chars.Tab);
				if (array4.Length == 2 && !(array4[1] != fileName))
				{
					if (!Sha.TryParse(array4[0], out var result))
					{
						return GitCommandResult<Sha>.Failure(new GitCommandError.ParseError("Failed to parse submodule SHA in '" + array3[1] + "'"));
					}
					return GitCommandResult<Sha>.Success(result);
				}
			}
			return GitCommandResult<Sha>.Failure(new GitCommandError.NotFound());
		}
	}
}
