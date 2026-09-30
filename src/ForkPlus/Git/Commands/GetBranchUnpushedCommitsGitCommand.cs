using System;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	// WS2.1 删除本地分支安全预览：统计分支相对 upstream 未推送的提交数。
	// upstream 非空：rev-list --count <upstream>..<branch>
	// upstream 为空：rev-list --count <branch> --not --remotes（按全部远程跟踪分支计）
	public class GetBranchUnpushedCommitsGitCommand
	{
		public GitCommandResult<int> Execute(GitModule gitModule, string branchFullRef, string upstreamFullRef)
		{
			GitRequest request = new GitRequest(gitModule);
			if (string.IsNullOrWhiteSpace(upstreamFullRef))
			{
				request = request.Command("rev-list", "--count", branchFullRef, "--not", "--remotes");
			}
			else
			{
				request = request.Command("rev-list", "--count", upstreamFullRef + ".." + branchFullRef);
			}
			GitRequestResult gitRequestResult = request.Execute();
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
