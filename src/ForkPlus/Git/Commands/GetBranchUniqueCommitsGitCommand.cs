using System;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	// WS2.1 删除本地分支安全预览：统计只在该分支、不在任何其他本地/远程分支的提交数。
	// 命令：rev-list --count <branch> --not --exclude=<branchName> --branches --remotes
	// 注意：--branches/--remotes 的 --exclude 匹配的是去掉 refs/heads//refs/remotes/ 前缀的
	// 短名（传 full ref 不生效，探针实证返回恒为 0），因此从 full ref 推导短名作 exclude。
	public class GetBranchUniqueCommitsGitCommand
	{
		public GitCommandResult<int> Execute(GitModule gitModule, string branchFullRef)
		{
			string branchName = branchFullRef;
			if (branchName != null && branchName.StartsWith("refs/heads/", StringComparison.Ordinal))
			{
				branchName = branchName.Substring("refs/heads/".Length);
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("rev-list", "--count", branchFullRef, "--not", "--exclude=" + branchName, "--branches", "--remotes").Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<int>.Failure(gitRequestResult.ToGitCommandError());
			}
			string text = gitRequestResult.Stdout.Trim();
			if (int.TryParse(text, out var result))
			{
				return GitCommandResult<int>.Success(result);
			}
			return GitCommandResult<int>.Failure(new GitCommandError.ParseError("Cannot parse '" + text + "'"));
		}
	}
}
