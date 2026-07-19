using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// Cherry-pick 冲突预检：用 git merge-tree 做无副作用的 3-way merge 预演，
	/// 检测把指定 commit(s) 应用到当前 HEAD 是否会产生冲突。
	/// 算法：对每个待 pick 的 sha，调 `git merge-tree <sha^> <HEAD> <sha>`，
	/// 若 stdout 含 `+<<<<<<<` 等冲突标记则判定会冲突。
	/// 多 commit 情况下采用简化策略：任一 commit 单独应用到当前 HEAD 会冲突即整体判为 Conflict。
	/// </summary>
	public class CherryPickTestGitCommand
	{
		public enum TestResult
		{
			Success,
			Conflict,
			Unknown
		}

		public GitCommandResult<TestResult> Execute(GitModule gitModule, Sha[] shas, int? parentNumber)
		{
			if (shas == null || shas.Length == 0)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			// 拿当前 HEAD
			GitRequestResult headResult = new GitRequest(gitModule).Command("rev-parse", "HEAD").Execute();
			if (!headResult.Success)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			string headSha = headResult.Stdout.Trim();
			if (headSha.Length != 40)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			// 逐个 commit 预检
			foreach (Sha sha in shas)
			{
				if (sha == null)
				{
					continue;
				}
				// base = sha 的父提交：merge commit 用 -m N 指定的父，否则用 sha^
				string baseRef = parentNumber.HasValue
					? sha.ToString() + "^" + parentNumber.Value.ToString()
					: sha.ToString() + "^";
				GitRequestResult baseResult = new GitRequest(gitModule)
					.Command("rev-parse", "--verify", baseRef + "^{commit}").Execute();
				if (!baseResult.Success)
				{
					return GitCommandResult<TestResult>.Success(TestResult.Unknown);
				}
				string baseSha = baseResult.Stdout.Trim();
				if (baseSha.Length != 40)
				{
					return GitCommandResult<TestResult>.Success(TestResult.Unknown);
				}
				try
				{
					// git merge-tree <base> <branch1> <branch2> 模拟 3-way merge
					// 这里 base=sha^, branch1=HEAD, branch2=sha，等价于 cherry-pick 的 3-way merge
					GitRequestResult mergeTreeResult = new GitRequest(gitModule)
						.Command("merge-tree", baseSha, headSha, sha.ToString()).Execute();
					if (!mergeTreeResult.Success)
					{
						return GitCommandResult<TestResult>.Success(TestResult.Unknown);
					}
					string stdout = mergeTreeResult.Stdout;
					if (stdout.Contains("+>>>>>>>") || stdout.Contains("+<<<<<<<")
						|| stdout.Contains("->>>>>>>") || stdout.Contains("-<<<<<<<"))
					{
						return GitCommandResult<TestResult>.Success(TestResult.Conflict);
					}
				}
				catch
				{
					return GitCommandResult<TestResult>.Success(TestResult.Unknown);
				}
			}
			return GitCommandResult<TestResult>.Success(TestResult.Success);
		}
	}
}
