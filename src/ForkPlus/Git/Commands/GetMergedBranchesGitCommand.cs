using System;
using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	// WS2.1 删除本地分支安全预览：列出已合并进基准分支的本地分支（full ref）。
	// 命令：for-each-ref --merged=<base> --format=%(refname) refs/heads/
	// 结果含 base 自身，调用方需排除自身与被删分支。
	public class GetMergedBranchesGitCommand
	{
		public GitCommandResult<string[]> Execute(GitModule gitModule, string baseRef)
		{
			if (string.IsNullOrWhiteSpace(baseRef))
			{
				baseRef = "HEAD";
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("for-each-ref", "--merged=" + baseRef, "--format=%(refname)", "refs/heads/").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<string[]>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			List<string> list = new List<string>(array.Length);
			string[] array2 = array;
			foreach (string text in array2)
			{
				string trimmed = text.Trim();
				if (trimmed.Length > 0)
				{
					list.Add(trimmed);
				}
			}
			return GitCommandResult<string[]>.Success(list.ToArray());
		}
	}
}
