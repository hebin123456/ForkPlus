using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class RebaseTestGitCommand
	{
		public enum TestResult
		{
			Success,
			Conflict,
			Unknown
		}

		public GitCommandResult<TestResult> Execute(GitModule gitModule, Reference src, string dst)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("merge-base", src.FullReference, dst).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			Sha? sha = Sha.Parse(gitRequestResult.Stdout.Trim());
			if (sha.HasValue)
			{
				Sha valueOrDefault = sha.GetValueOrDefault();
				if (src.Sha == valueOrDefault)
				{
					return GitCommandResult<TestResult>.Success(TestResult.Success);
				}
				// v4.0.5：git replay 需 git ≥2.44（Ubuntu 24.04 LTS 的 2.43 都没有）。
				// 老版本上 replay 不存在 → stderr "git: 'replay' is not a git command"
				// → 预检整体 Failure，变基弹窗直接报错不可用。按能力降级为旧式三参数
				// merge-tree 终态三方合并预演（CherryPick/Revert 预检同款方案）。
				if (GitCapabilities.SupportsReplay())
				{
					return ExecuteWithReplay(gitModule, valueOrDefault, src, dst);
				}
				return ExecuteWithMergeTree(gitModule, valueOrDefault, src, dst);
			}
			return GitCommandResult<TestResult>.Success(TestResult.Unknown);
		}

		/// <summary>git ≥2.44：replay 逐提交精确重放预演。</summary>
		private static GitCommandResult<TestResult> ExecuteWithReplay(GitModule gitModule, Sha mergeBase, Reference src, string dst)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("replay", "--onto", dst, mergeBase.ToString() + ".." + src.Sha).Execute();
			if (!gitRequestResult.Success)
			{
				if (gitRequestResult.Stderr.Trim() == "")
				{
					return GitCommandResult<TestResult>.Success(TestResult.Conflict);
				}
				return GitCommandResult<TestResult>.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult<TestResult>.Success(TestResult.Success);
		}

		/// <summary>git &lt;2.44 降级：旧式三参数 merge-tree 模拟把 src 树合到 dst 树
		/// （base=merge-base），stdout 含冲突标记判 Conflict。与 CherryPickTestGitCommand /
		/// RevertTestGitCommand 的检测方案一致（该命令自 git 1.4 时代可用）。</summary>
		private static GitCommandResult<TestResult> ExecuteWithMergeTree(GitModule gitModule, Sha mergeBase, Reference src, string dst)
		{
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command("merge-tree", mergeBase.ToString(), dst, src.Sha.ToString()).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			string stdout = gitRequestResult.Stdout;
			if (stdout.Contains("+>>>>>>>") || stdout.Contains("+<<<<<<<")
				|| stdout.Contains("->>>>>>>") || stdout.Contains("-<<<<<<<"))
			{
				return GitCommandResult<TestResult>.Success(TestResult.Conflict);
			}
			return GitCommandResult<TestResult>.Success(TestResult.Success);
		}
	}
}
