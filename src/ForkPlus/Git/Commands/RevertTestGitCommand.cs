using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// Revert 冲突预检：用 git merge-tree 做无副作用的 3-way merge 预演，
	/// 检测把指定 commit 的反向变更应用到当前 HEAD 是否会产生冲突。
	/// 算法：revert 等价于 HEAD 和 sha^ 的 3-way merge（base=sha, ours=HEAD, theirs=sha^），
	/// 调 `git merge-tree <sha> <HEAD> <sha^>`，若 stdout 含冲突标记则判为 Conflict。
	/// </summary>
	public class RevertTestGitCommand
	{
		public enum TestResult
		{
			Success,
			Conflict,
			Unknown
		}

		public GitCommandResult<TestResult> Execute(GitModule gitModule, Sha sha, int? parentNumber)
		{
			if (sha == null)
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
			// 拿 sha 的父提交（revert 的"目标内容"）
			string parentRef = parentNumber.HasValue
				? sha.ToString() + "^" + parentNumber.Value.ToString()
				: sha.ToString() + "^";
			GitRequestResult parentResult = new GitRequest(gitModule)
				.Command("rev-parse", "--verify", parentRef + "^{commit}").Execute();
			if (!parentResult.Success)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			string parentSha = parentResult.Stdout.Trim();
			if (parentSha.Length != 40)
			{
				return GitCommandResult<TestResult>.Success(TestResult.Unknown);
			}
			try
			{
				// git merge-tree <base> <branch1> <branch2> 模拟 3-way merge
				// revert 等价于 base=sha, ours=HEAD, theirs=sha^（反向应用 sha..sha^ 的 diff）
				GitRequestResult mergeTreeResult = new GitRequest(gitModule)
					.Command("merge-tree", sha.ToString(), headSha, parentSha).Execute();
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
			return GitCommandResult<TestResult>.Success(TestResult.Success);
		}
	}
}
